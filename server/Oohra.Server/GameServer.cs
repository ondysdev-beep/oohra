using System.Runtime.CompilerServices;
using LiteNetLib;
using LiteNetLib.Utils;
using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Plants;
using Oohra.Core.Rng;
using Oohra.Core.Time;
using Oohra.Core.World;
using Oohra.Protocol;

[assembly: InternalsVisibleTo("Oohra.Server.Tests")]

namespace Oohra.Server;

/// <summary>
/// Autoritativní server: drží svět, čas a všechny postavy. Klienti posílají jen úmysly
/// (jdi, utrhni, sněz), server rozhodne podle pravidel z Oohra.Core a pošle výsledek.
/// Skrytá data (složení rostlin) server nikdy neposílá.
/// Použití: Start, potom pořád dokola Poll (příjem zpráv) a Tick (krok simulace).
/// </summary>
public sealed class GameServer : IDisposable
{
    private const int MinNicknameLength = 2;
    private const int MaxNicknameLength = 16;

    private readonly ServerConfig _config;
    private readonly WorldView _worldView;
    private readonly NetManager _net;
    private readonly NetDataWriter _writer = new();
    private readonly Dictionary<int, PlayerSession> _byPeer = [];
    private readonly Dictionary<string, PlayerSession> _byNickname = new(StringComparer.OrdinalIgnoreCase);
    private readonly Mulberry32 _rng;
    private readonly PlantVisual[] _sentVisuals;
    private int _nextPlayerId = 1;
    private long _tick;
    private int _sentFiresVersion;
    private int _sentStructuresVersion;

    public GameServer(ServerConfig config)
    {
        _config = config;
        var size = config.WorldSize > 0 ? config.WorldSize : GameData.Settings.WorldWidth;
        Island = IslandGenerator.Build(config.Seed, size, size);
        Clock = GameClock.FromSettings(config.StartHour * 60);
        Plants = new PlantStates();
        _worldView = WorldViews.From(Island);
        _rng = new Mulberry32(config.Seed ^ 0x5E4E);
        _sentVisuals = new PlantVisual[Island.Plants.Count];
        Animals = new Animals(config.Seed) { Enabled = config.Animals };
        if (config.Animals)
        {
            Animals.Populate(Island, []);
        }

        var listener = new EventBasedNetListener();
        listener.ConnectionRequestEvent += OnConnectionRequest;
        listener.PeerDisconnectedEvent += (peer, info) => OnDisconnected(peer);
        listener.NetworkReceiveEvent += (peer, reader, channel, method) => OnReceive(peer, reader);
        _net = new NetManager(listener) { AutoRecycle = true, EnableStatistics = true };
    }

    /// <summary>Zpráva do konzole serveru (kdo přišel, kdo odešel).</summary>
    public Action<string>? Log { get; set; }

    public int Port => _net.LocalPort;

    public int OnlinePlayers => _byPeer.Count;

    /// <summary>Kolik bajtů server celkem odeslal (pro měření zátěže).</summary>
    public long BytesSent => _net.Statistics.BytesSent;

    internal Island Island { get; }

    internal GameClock Clock { get; }

    internal PlantStates Plants { get; }

    internal Fires Fires { get; } = new();

    internal Materials Materials { get; } = new();

    internal Animals Animals { get; }

    /// <summary>Spustí server. Port 0 = vyber volný port (pro testy).</summary>
    public void Start(int port)
    {
        _net.Start(port);
    }

    /// <summary>Přijme zprávy od klientů a zpracuje je.</summary>
    public void Poll()
    {
        _net.PollEvents();
    }

    /// <summary>Jeden krok simulace: čas, pohyb, tělo, a pravidelné zprávy klientům.</summary>
    public void Tick(double realSeconds)
    {
        var minutes = Clock.Advance(realSeconds * _config.TimeSpeed);
        Fires.Burn(minutes);
        Animals.Update(Island, Fires, Plants, Clock, realSeconds, AnimalTargets());

        foreach (var player in _byPeer.Values)
        {
            Move(player, realSeconds);
            Physiology.Update(player.Character, Clock, minutes, Fires.WarmsAt(Island, player.X, player.Y), Shelter.IsSheltered(Island, player.X, player.Y));
            SendBodyEvents(player);
        }

        if (Fires.Version != _sentFiresVersion)
        {
            _sentFiresVersion = Fires.Version;
            Broadcast(w => Messages.WriteFires(w, FireViews()));
        }

        if (Island.Structures.Version != _sentStructuresVersion)
        {
            _sentStructuresVersion = Island.Structures.Version;
            Broadcast(w => Messages.WriteStructures(w, StructureViews()));
        }

        _tick++;
        if (_tick % _config.SnapshotEveryTicks == 0)
        {
            BroadcastSnapshot();
            SendAnimals();
        }

        if (_tick % _config.SelfStateEveryTicks == 0)
        {
            foreach (var player in _byPeer.Values)
            {
                SendSelfState(player);
            }
        }

        if (_tick % _config.PlantStatesEveryTicks == 0)
        {
            BroadcastPlantChanges();
            foreach (var player in _byPeer.Values)
            {
                if (SpoilageOf(player) != player.SentSpoilage)
                {
                    SendInventory(player);
                }
            }
        }
    }

    public void Dispose()
    {
        _net.Stop();
    }

    internal PlayerSession? FindPlayer(string nickname)
    {
        return _byNickname.TryGetValue(nickname, out var player) ? player : null;
    }

    // ---------- spojení a přihlášení ----------

    private void OnConnectionRequest(ConnectionRequest request)
    {
        if (_net.ConnectedPeersCount >= _config.MaxPlayers)
        {
            request.Reject();
            return;
        }

        request.AcceptIfKey(ProtocolInfo.ConnectionKey);
    }

    private void OnDisconnected(NetPeer peer)
    {
        if (!_byPeer.Remove(peer.Id, out var player))
        {
            return;
        }

        player.Peer = null;
        player.InputX = player.InputY = 0;
        Broadcast(w => Messages.WritePlayerLeft(w, player.Id));
        Log?.Invoke($"{player.Nickname} odešel.");
    }

    private void HandleLogin(NetPeer peer, NetDataReader reader)
    {
        var (version, inviteCode, rawNickname) = Messages.ReadLogin(reader);
        var nickname = rawNickname.Trim();
        var error = LoginError(version, inviteCode, nickname);
        if (error is not null)
        {
            SendTo(peer, w => Messages.WriteLoginResult(w, new LoginResult(false, error, -1)));
            return;
        }

        var player = FindPlayer(nickname) ?? CreatePlayer(nickname);
        player.Peer = peer;
        _byPeer[peer.Id] = player;

        SendTo(peer, w => Messages.WriteLoginResult(w, new LoginResult(true, string.Empty, player.Id)));
        SendTo(peer, w => Messages.WriteWorld(w, _worldView));
        foreach (var other in _byPeer.Values)
        {
            SendTo(peer, w => Messages.WritePlayerJoined(w, InfoOf(other)));
        }

        Broadcast(w => Messages.WritePlayerJoined(w, InfoOf(player)), except: peer);
        SendTo(peer, w => Messages.WritePlantStates(w, AllPlantVisuals()));
        SendTo(peer, w => Messages.WriteFires(w, FireViews()));
        SendTo(peer, w => Messages.WriteStructures(w, StructureViews()));
        SendInventory(player);
        SendSelfState(player);
        Log?.Invoke($"{player.Nickname} přišel (hráčů ve hře: {_byPeer.Count}).");
    }

    private string? LoginError(int version, string inviteCode, string nickname)
    {
        if (version != ProtocolInfo.Version)
        {
            return "Máš jinou verzi hry než server.";
        }

        if (!_config.InviteCodes.Contains(inviteCode.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return "Neplatný kód pozvánky.";
        }

        if (nickname.Length < MinNicknameLength || nickname.Length > MaxNicknameLength
            || !nickname.All(ch => char.IsLetterOrDigit(ch) || ch is ' ' or '-' or '_'))
        {
            return $"Přezdívka musí mít {MinNicknameLength} až {MaxNicknameLength} písmen nebo číslic.";
        }

        if (FindPlayer(nickname) is { Online: true })
        {
            return "Hráč s touto přezdívkou už je ve hře.";
        }

        return null;
    }

    /// <summary>Nový hráč: první život je Pradávný, narodí se v divočině u vody.</summary>
    private PlayerSession CreatePlayer(string nickname)
    {
        var character = new Character(0, new Mulberry32(_rng.NextInt(int.MaxValue)));
        var spawn = SpawnPoint.PickWild(Island, _rng, firstLife: true);
        var player = new PlayerSession(_nextPlayerId++, nickname, character, NewInventory(character), spawn.X, spawn.Y);
        _byNickname[nickname] = player;
        return player;
    }

    /// <summary>Prázdný batoh, při zkoušení (startSticks, startStones v server.json) s materiálem na oheň.</summary>
    private Inventory NewInventory(Character character)
    {
        var inventory = Inventory.For(character.Traits);
        for (var i = 0; i < _config.StartSticks; i++)
        {
            inventory.TryAdd(Item.Stick);
        }

        for (var i = 0; i < _config.StartStones; i++)
        {
            inventory.TryAdd(Item.Stone);
        }

        return inventory;
    }

    // ---------- úmysly hráčů ----------

    private void OnReceive(NetPeer peer, NetDataReader reader)
    {
        var type = (MessageType)reader.GetByte();
        if (type == MessageType.Login)
        {
            if (!_byPeer.ContainsKey(peer.Id))
            {
                HandleLogin(peer, reader);
            }

            return;
        }

        if (!_byPeer.TryGetValue(peer.Id, out var player))
        {
            return;
        }

        if (player.Character.Sleeping && type is not (MessageType.Input or MessageType.Sleep))
        {
            // Spící postava nic nedělá. Probudí ji pohyb nebo tlačítko Vstát.
            return;
        }

        switch (type)
        {
            case MessageType.Input:
                SetInput(player, Messages.ReadInput(reader));
                break;
            case MessageType.Sleep:
                ToggleSleep(player);
                break;
            case MessageType.Harvest:
                Harvest(player, Messages.ReadHarvest(reader));
                break;
            case MessageType.Drink:
                Drink(player, Messages.ReadDrink(reader));
                break;
            case MessageType.Eat:
            case MessageType.Taste:
            case MessageType.Drop:
                UseStack(player, type, Messages.ReadStackAction(reader));
                break;
            case MessageType.Reborn:
                Reborn(player);
                break;
            case MessageType.GatherSticks:
            case MessageType.TakeStone:
                GatherMaterial(player, type, Messages.ReadTileAction(reader));
                break;
            case MessageType.FeedFire:
                var fireId = Messages.ReadFeedFire(reader);
                FireAction(player, () => Fires.Feed(fireId, player.Inventory, player.X, player.Y));
                break;
            case MessageType.Bake:
                var stackIndex = Messages.ReadStackAction(reader);
                FireAction(player, () => Fires.Bake(Island, player.Inventory, stackIndex, player.X, player.Y, player.Character));
                break;
            case MessageType.Place:
                Place(player, Messages.ReadPlace(reader));
                break;
            case MessageType.RemoveStructure:
                var structureId = Messages.ReadRemoveStructure(reader);
                BuildAction(player, () => Island.Structures.Remove(structureId, player.Inventory, player.X, player.Y));
                break;
            case MessageType.Attack:
                var target = Messages.ReadAnimalAction(reader);
                HuntAction(player, target, () => Animals.Attack(target, new AnimalTarget(player.Id, player.X, player.Y, player.Character), player.Inventory, Clock.TotalMinutes));
                break;
            case MessageType.Butcher:
                var corpse = Messages.ReadAnimalAction(reader);
                HuntAction(player, corpse, () => Animals.Butcher(corpse, player.Inventory, player.X, player.Y, Clock.TotalMinutes));
                break;
            case MessageType.MakeSpear:
                HuntAction(player, -1, () => Crafting.MakeSpear(player.Inventory, player.Character));
                break;
            case MessageType.Fish:
                var (waterX, waterY) = Messages.ReadTileAction(reader);
                HuntAction(player, -1, () => Animals.Fish(Island, waterX, waterY, new AnimalTarget(player.Id, player.X, player.Y, player.Character), player.Inventory, Clock.TotalMinutes));
                break;
            case MessageType.TakeCatch:
                var trapId = Messages.ReadAnimalAction(reader);
                HuntAction(player, -1, () => Island.Structures.TakeCatch(trapId, player.Inventory, player.X, player.Y, Clock.TotalMinutes));
                break;
        }
    }

    /// <summary>Režim stavby: zeď jde do staveb, oheň mezi ohně. Kam a pod jakým úhlem, určil hráč.</summary>
    private void Place(PlayerSession player, (BuildKind Kind, float X, float Y, float Angle, float Length) place)
    {
        if (place.Kind == BuildKind.Campfire)
        {
            FireAction(player, () => Fires.Light(Island, player.Inventory, player.X, player.Y, place.X, place.Y, player.Character));
            return;
        }

        BuildAction(player, () => Island.Structures.Build(
            Island, Fires, player.Inventory, player.X, player.Y, place.Kind, place.X, place.Y, place.Angle, place.Length, PlayerPositions(), player.Character));
    }

    /// <summary>Lov, stažení masa, výroba kopí. Hláška nese druh zvířete (pro text „Divočák padl.“).</summary>
    private void HuntAction(PlayerSession player, int animalId, Func<HuntResult> action)
    {
        if (player.Character.IsDead)
        {
            return;
        }

        var kind = Animals.Find(animalId)?.Kind ?? AnimalKind.Rabbit;
        var result = action();
        SendEvent(player, new GameEvent(EventKind.Hunt, (byte)result, default, [], Subject: (byte)kind));
        SendInventory(player);
        SendSelfState(player);
    }

    /// <summary>Stavba: postavit nebo rozebrat. Seznam staveb se všem pošle v příštím ticku.</summary>
    private void BuildAction(PlayerSession player, Func<BuildResult> action)
    {
        if (player.Character.IsDead)
        {
            return;
        }

        var result = action();
        SendEvent(player, new GameEvent(EventKind.Build, (byte)result, default, []));
        SendInventory(player);
    }

    /// <summary>Kde stojí hráči ve hře (zeď se nesmí postavit na nikoho z nich).</summary>
    private List<Position> PlayerPositions()
    {
        return _byPeer.Values.Select(p => new Position(p.X, p.Y)).ToList();
    }

    private void GatherMaterial(PlayerSession player, MessageType type, (int TileX, int TileY) tile)
    {
        if (player.Character.IsDead)
        {
            return;
        }

        var result = type == MessageType.GatherSticks
            ? Materials.GatherSticks(Island, tile.TileX, tile.TileY, Clock, player.Inventory, _rng, player.X, player.Y)
            : Materials.TakeStone(Island, tile.TileX, tile.TileY, player.Inventory, player.X, player.Y);
        SendEvent(player, new GameEvent(EventKind.Material, (byte)result, default, []));
        if (result is MaterialResult.GotSticks or MaterialResult.GotStone)
        {
            SendInventory(player);
        }
    }

    /// <summary>Oheň: rozdělat, přiložit nebo péct. Seznam ohňů se ostatním pošle v příštím ticku.</summary>
    private void FireAction(PlayerSession player, Func<FireResult> action)
    {
        if (player.Character.IsDead)
        {
            return;
        }

        var result = action();
        SendEvent(player, new GameEvent(EventKind.Fire, (byte)result, default, []));
        SendInventory(player);
    }

    /// <summary>Směr chůze. Když spící postava dostane pokyn k chůzi, probudí se.</summary>
    private void SetInput(PlayerSession player, (float DirX, float DirY) input)
    {
        var length = Math.Sqrt(input.DirX * input.DirX + input.DirY * input.DirY);
        player.InputX = length > 0.01 ? input.DirX / length : 0;
        player.InputY = length > 0.01 ? input.DirY / length : 0;
        if (length > 0.01 && player.Character.Sleeping)
        {
            player.Character.WakeUp(Clock.TotalMinutes, BodyEvent.WokeUp);
            SendBodyEvents(player);
            SendSelfState(player);
        }
    }

    /// <summary>Spát, nebo vstát. Usnout jde jen unavený a v teple (rozhodne Core).</summary>
    private void ToggleSleep(PlayerSession player)
    {
        var c = player.Character;
        if (c.IsDead)
        {
            return;
        }

        if (c.Sleeping)
        {
            c.WakeUp(Clock.TotalMinutes, BodyEvent.WokeUp);
        }
        else if (c.TryFallAsleep(Clock.TotalMinutes))
        {
            player.InputX = player.InputY = 0;
        }

        SendBodyEvents(player);
        SendSelfState(player);
    }

    private void Harvest(PlayerSession player, (int PlantId, PlantPart Part) request)
    {
        if (player.Character.IsDead || request.PlantId < 0 || request.PlantId >= Island.Plants.Count)
        {
            return;
        }

        var result = Gathering.Harvest(Island, Plants, Island.Plants[request.PlantId], request.Part, Clock, player.Inventory, player.X, player.Y, player.Character);
        SendEvent(player, new GameEvent(EventKind.Harvest, (byte)result, request.Part, []));
        if (result == HarvestResult.Picked)
        {
            SendInventory(player);
            BroadcastPlantChanges();
        }
    }

    private void Drink(PlayerSession player, (int TileX, int TileY) request)
    {
        if (player.Character.IsDead)
        {
            return;
        }

        var result = Drinking.Drink(player.Character, Island, request.TileX, request.TileY, player.X, player.Y, Clock);
        SendEvent(player, new GameEvent(EventKind.Drink, (byte)result, default, []));
        SendSelfState(player);
    }

    private void UseStack(PlayerSession player, MessageType action, int index)
    {
        var inventory = player.Inventory;
        if (player.Character.IsDead || index < 0 || index >= inventory.Stacks.Count)
        {
            return;
        }

        var stack = inventory.Stacks[index].Item;
        var item = stack.Plant;
        var spoiled = Spoilage.IsSpoiled(stack, Clock.TotalMinutes);
        switch (action)
        {
            case MessageType.Eat when item is not null:
                inventory.TakeOne(index);
                Eating.Eat(player.Character, item, Island.SpeciesSecrets[item.SpeciesId], Clock, spoiled);
                break;
            case MessageType.Eat when stack.IsFlesh:
                inventory.TakeOne(index);
                Eating.EatMeat(player.Character, stack.Cooking, Clock, spoiled: spoiled);
                break;
            case MessageType.Taste when stack.IsFlesh:
                Eating.EatMeat(player.Character, stack.Cooking, Clock, taste: true, spoiled: spoiled);
                break;
            case MessageType.Taste when item is not null:
                Eating.Taste(player.Character, item, Island.SpeciesSecrets[item.SpeciesId], Clock, spoiled);
                break;
            case MessageType.Drop:
                inventory.Drop(index);
                break;
        }

        SendInventory(player);
        SendBodyEvents(player);
        SendSelfState(player);
    }

    /// <summary>Po smrti: Nový o generaci výš, jinde na ostrově, s prázdným batohem.</summary>
    private void Reborn(PlayerSession player)
    {
        if (!player.Character.IsDead)
        {
            return;
        }

        var generation = GenerationTraits.NextAfterDeath(player.Character.Traits.Generation);
        var deathPlace = new Position(player.X, player.Y);
        player.Lives++;
        player.Character = new Character(generation, new Mulberry32(_rng.NextInt(int.MaxValue)));
        player.Inventory = NewInventory(player.Character);
        var spawn = SpawnPoint.PickWild(Island, _rng, firstLife: false, avoid: deathPlace);
        player.X = spawn.X;
        player.Y = spawn.Y;
        player.InputX = player.InputY = 0;

        Broadcast(w => Messages.WritePlayerJoined(w, InfoOf(player)));
        SendInventory(player);
        SendSelfState(player);
    }

    // ---------- simulace ----------

    private void Move(PlayerSession player, double realSeconds)
    {
        var c = player.Character;
        if (c.IsDead || c.Sleeping || c.FaintMinutes > 0 || c.StunSeconds > 0 || (player.InputX == 0 && player.InputY == 0))
        {
            return;
        }

        var distance = Movement.Distance(Island, c, player.X, player.Y, realSeconds);
        var (x, y) = Movement.Step(Island, player.X, player.Y, player.InputX, player.InputY, distance);
        player.X = x;
        player.Y = y;
        player.Facing = Math.Atan2(player.InputY, player.InputX);
    }

    // ---------- zprávy klientům ----------

    private void SendBodyEvents(PlayerSession player)
    {
        foreach (var message in player.Character.TakeMessages())
        {
            SendEvent(player, new GameEvent(EventKind.Body, (byte)message.Event, default, message.Notes?.ToArray() ?? [], (byte)(message.Skill ?? 0), (byte)message.Level));
        }
    }

    private void SendEvent(PlayerSession player, GameEvent gameEvent)
    {
        if (player.Peer is not null)
        {
            SendTo(player.Peer, w => Messages.WriteEvent(w, gameEvent));
        }
    }

    private void SendSelfState(PlayerSession player)
    {
        if (player.Peer is null)
        {
            return;
        }

        var c = player.Character;
        var state = new SelfState(
            (float)c.Satiety,
            (float)c.Hydration,
            (float)c.Energy,
            (float)c.Warmth,
            (float)c.Health,
            c.Symptoms().ToArray(),
            c.Traits.Generation,
            c.IsDead,
            c.DeathCause,
            Fires.WarmsAt(Island, player.X, player.Y),
            Shelter.IsSheltered(Island, player.X, player.Y),
            c.Sleeping,
            SkillKinds.Select(s => (byte)c.Skills.Level(s)).ToArray(),
            PendingSkills(c));
        SendTo(player.Peer, w => Messages.WriteSelfState(w, state));
    }

    private void SendInventory(PlayerSession player)
    {
        if (player.Peer is null)
        {
            return;
        }

        var stacks = player.Inventory.Stacks.Select(StackViewOf).ToArray();
        var view = new InventoryView(player.Inventory.Capacity, stacks);
        player.SentSpoilage = SpoilageOf(player);
        SendTo(player.Peer, w => Messages.WriteInventory(w, view));
    }

    /// <summary>Které hromádky jsou zkažené (jako text, třeba „01“). Když se změní, batoh se pošle znovu.</summary>
    private string SpoilageOf(PlayerSession player)
    {
        return string.Concat(player.Inventory.Stacks.Select(s => Spoilage.IsSpoiled(s.Item, Clock.TotalMinutes) ? '1' : '0'));
    }

    private void BroadcastSnapshot()
    {
        var players = _byPeer.Values
            .Select(p => new PlayerSnapshot(p.Id, (float)p.X, (float)p.Y, (float)p.Facing, p.Character.IsDead, p.Character.Sleeping))
            .ToArray();
        var snapshot = new Snapshot(Clock.TotalMinutes, Clock.CalendarMonths, players);
        Broadcast(w => Messages.WriteSnapshot(w, snapshot), DeliveryMethod.Sequenced);
    }

    /// <summary>Pošle všem rostliny, které od minula změnily vzhled (plody, vykopání, dorostení).</summary>
    private void BroadcastPlantChanges()
    {
        var changes = new List<PlantStateChange>();
        foreach (var plant in Island.Plants)
        {
            var visual = WorldViews.PlantVisual(Island, Plants, plant, Clock);
            if (visual != _sentVisuals[plant.Id])
            {
                _sentVisuals[plant.Id] = visual;
                changes.Add(new PlantStateChange(plant.Id, visual));
            }
        }

        if (changes.Count > 0)
        {
            Broadcast(w => Messages.WritePlantStates(w, changes));
        }
    }

    private List<PlantStateChange> AllPlantVisuals()
    {
        return Island.Plants
            .Select(p => new PlantStateChange(p.Id, WorldViews.PlantVisual(Island, Plants, p, Clock)))
            .ToList();
    }

    private static readonly SkillKind[] SkillKinds = [SkillKind.Gather, SkillKind.Hunt, SkillKind.Craft, SkillKind.Build];

    /// <summary>Bity dovedností, jejichž nácvik čeká na spánek (bit = číslo dovednosti).</summary>
    private static byte PendingSkills(Character c)
    {
        var bits = 0;
        foreach (var skill in SkillKinds)
        {
            if (c.Skills.Pending(skill) > 0)
            {
                bits |= 1 << (int)skill;
            }
        }

        return (byte)bits;
    }

    /// <summary>Hráči ve hře, jak je vidí zvířata.</summary>
    private List<AnimalTarget> AnimalTargets()
    {
        return _byPeer.Values.Select(p => new AnimalTarget(p.Id, p.X, p.Y, p.Character)).ToList();
    }

    /// <summary>Každému hráči zvířata v jeho okolí (animalViewDistance), ať se neposílá celý ostrov.</summary>
    private void SendAnimals()
    {
        if (!_config.Animals)
        {
            return;
        }

        var range = _config.AnimalViewDistance;
        foreach (var player in _byPeer.Values)
        {
            if (player.Peer is null)
            {
                continue;
            }

            var near = Animals.All
                .Where(a => Math.Abs(a.X - player.X) < range && Math.Abs(a.Y - player.Y) < range)
                .Select(a => new AnimalView(a.Id, a.Kind, (float)a.X, (float)a.Y, (float)a.Dir, a.State))
                .ToArray();
            SendTo(player.Peer, w => Messages.WriteAnimals(w, near), DeliveryMethod.Sequenced);
        }
    }

    private StackView StackViewOf(ItemStack stack)
    {
        var plant = stack.Item.Plant;
        var spoiled = Spoilage.IsSpoiled(stack.Item, Clock.TotalMinutes);
        return plant is null
            ? new StackView(stack.Item.Kind, 0, default, default, stack.Item.Cooking, stack.Count, spoiled)
            : new StackView(stack.Item.Kind, plant.SpeciesId, plant.Part, plant.Ripeness, plant.Cooking, stack.Count, spoiled);
    }

    private FireView[] FireViews()
    {
        return Fires.All.Select(f => new FireView(f.Id, (float)f.X, (float)f.Y, f.Burning)).ToArray();
    }

    private StructureView[] StructureViews()
    {
        return Island.Structures.All.Select(s => new StructureView(s.Id, s.Kind, (float)s.X, (float)s.Y, (float)s.Angle, (float)s.Length, s.Caught.HasValue ? (int)s.Caught.Value : -1)).ToArray();
    }

    private static PlayerInfo InfoOf(PlayerSession player)
    {
        return new PlayerInfo(player.Id, player.Nickname, player.Character.Traits.Generation);
    }

    private void SendTo(NetPeer peer, Action<NetDataWriter> write, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
    {
        _writer.Reset();
        write(_writer);
        peer.Send(_writer, method);
    }

    private void Broadcast(Action<NetDataWriter> write, DeliveryMethod method = DeliveryMethod.ReliableOrdered, NetPeer? except = null)
    {
        foreach (var player in _byPeer.Values)
        {
            if (player.Peer is not null && player.Peer != except)
            {
                SendTo(player.Peer, write, method);
            }
        }
    }

    private void Broadcast(Action<NetDataWriter> write, NetPeer except)
    {
        Broadcast(write, DeliveryMethod.ReliableOrdered, except);
    }
}
