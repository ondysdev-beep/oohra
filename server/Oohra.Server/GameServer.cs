using System.Runtime.CompilerServices;
using LiteNetLib;
using LiteNetLib.Utils;
using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Language;
using Oohra.Core.Plants;
using Oohra.Core.Rng;
using Oohra.Core.Social;
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
    private int _sentSignsVersion;
    private readonly IReadOnlyDictionary<Concept, byte[]> _commonWords;
    private double _seconds;
    private string _stockSpoilage = string.Empty;

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
        _commonWords = CommonWords.For(config.Seed);
        Island.Signs.AddAncient(Island);
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

    /// <summary>Kmeny hráčů (patří hráči, ne postavě, takže přežijí smrt).</summary>
    internal Tribes Tribes { get; } = new();

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
        _seconds += realSeconds;
        var minutes = Clock.Advance(realSeconds * _config.TimeSpeed);
        Fires.Burn(minutes);
        Animals.Update(Island, Fires, Plants, Clock, realSeconds, AnimalTargets());

        foreach (var player in _byPeer.Values)
        {
            Move(player, realSeconds);
            Physiology.Update(player.Character, Clock, minutes, Fires.WarmsAt(Island, player.X, player.Y), Shelter.IsSheltered(Island, player.X, player.Y));
            SendBodyEvents(player);
            if (player.Character.Vocabulary.Version != player.SentVocabularyVersion)
            {
                SendVocabulary(player);
            }

            SendTribeState(player);
        }

        if (Fires.Version != _sentFiresVersion)
        {
            _sentFiresVersion = Fires.Version;
            Broadcast(w => Messages.WriteFires(w, FireViews()));
        }

        Island.Signs.Weather(Clock.TotalMinutes);
        if (Island.Signs.Version != _sentSignsVersion)
        {
            _sentSignsVersion = Island.Signs.Version;
            Broadcast(w => Messages.WriteSigns(w, SignViews()));
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
            _stockSpoilage = StockSpoilage();
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
        SendTo(peer, w => Messages.WriteSigns(w, SignViews()));
        SendTo(peer, w => Messages.WriteDecorationTypes(w, DecorationTypeViews()));
        player.SentTribeVersion = -1;
        player.SentStockKey = string.Empty;
        player.SentVocabularyVersion = -1;
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
        var character = NewCharacter(0);
        var spawn = SpawnPoint.PickWild(Island, _rng, firstLife: true);
        var player = new PlayerSession(_nextPlayerId++, nickname, character, NewInventory(character), spawn.X, spawn.Y);
        _byNickname[nickname] = player;
        return player;
    }

    /// <summary>Nová postava. Pradávný zná od narození slova Starého hlasu.</summary>
    private Character NewCharacter(int generation)
    {
        var character = new Character(generation, new Mulberry32(_rng.NextInt(int.MaxValue)));
        if (character.Traits.IsAncient)
        {
            character.Vocabulary.GrantOldVoice(_commonWords);
        }

        return character;
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
                BuildAction(player, () => Island.Structures.Remove(structureId, player.Inventory, player.X, player.Y, player.Id, Tribes));
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
                HuntAction(player, -1, () => Island.Structures.TakeCatch(trapId, player.Inventory, player.X, player.Y, Clock.TotalMinutes, player.Id, Tribes));
                break;
            case MessageType.Say:
                Say(player, Messages.ReadSay(reader));
                break;
            case MessageType.Gesture:
                MakeGesture(player, Messages.ReadGesture(reader));
                break;
            case MessageType.DrawSign:
                var (signX, signY, bits) = Messages.ReadDrawSign(reader);
                SignAction(player, () => Island.Signs.Draw(Island, player.Id, player.X, player.Y, signX, signY, bits, Clock.TotalMinutes));
                break;
            case MessageType.EraseSign:
                var signId = Messages.ReadEraseSign(reader);
                SignAction(player, () => Island.Signs.Erase(signId, player.Id, player.X, player.Y, Tribes));
                break;
            case MessageType.InviteToTribe:
                InviteToTribe(player, Messages.ReadInviteToTribe(reader));
                break;
            case MessageType.AnswerInvite:
                AnswerInvite(player, Messages.ReadAnswerInvite(reader));
                break;
            case MessageType.LeaveTribe:
                SendEvent(player, TribeEvent(Tribes.Leave(player.Id)));
                break;
            case MessageType.StoreItem:
                var (storeId, storeIndex) = Messages.ReadStockAction(reader);
                StockAction(player, () => Island.Structures.StoreItem(storeId, player.Inventory, storeIndex, player.X, player.Y, player.Id, Tribes));
                break;
            case MessageType.TakeItem:
                var (takeId, takeIndex) = Messages.ReadStockAction(reader);
                StockAction(player, () => Island.Structures.TakeItem(takeId, player.Inventory, takeIndex, player.X, player.Y, player.Id, Tribes));
                break;
            case MessageType.SetMeaning:
                var (word, meaning) = Messages.ReadSetMeaning(reader);
                if (Words.IsValid(word) && (meaning is null || Enum.IsDefined(meaning.Value)))
                {
                    player.Character.Vocabulary.SetMeaning(word, meaning);
                }

                break;
        }
    }

    /// <summary>Režim stavby: zeď jde do staveb, oheň mezi ohně. Kam a pod jakým úhlem, určil hráč.</summary>
    private void Place(PlayerSession player, (BuildKind Kind, float X, float Y, float Angle, float Length, string Decoration) place)
    {
        if (place.Kind == BuildKind.Campfire)
        {
            FireAction(player, () => Fires.Light(Island, player.Inventory, player.X, player.Y, place.X, place.Y, player.Character));
            return;
        }

        BuildAction(player, () => Island.Structures.Build(
            Island, Fires, player.Inventory, player.X, player.Y, place.Kind, place.X, place.Y, place.Angle, place.Length, PlayerPositions(), player.Character, player.Id, place.Decoration));
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

    private void SignAction(PlayerSession player, Func<SignResult> action)
    {
        if (!player.Character.IsDead)
        {
            SendEvent(player, new GameEvent(EventKind.Sign, (byte)action(), default, []));
        }
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
        player.Character = NewCharacter(generation);
        player.SentVocabularyVersion = -1;
        player.Inventory = NewInventory(player.Character);
        var spawn = SpawnPoint.PickWild(Island, _rng, firstLife: false, avoid: deathPlace);
        player.X = spawn.X;
        player.Y = spawn.Y;
        player.InputX = player.InputY = 0;

        Broadcast(w => Messages.WritePlayerJoined(w, InfoOf(player)));
        SendInventory(player);
        SendSelfState(player);
    }

    // ---------- jazyk a gesta ----------

    /// <summary>
    /// Hráč řekl slovo. Uslyší ho hráči do hearDistance (i on sám, kvůli bublině). Kdo mluvčího vidí zblízka,
    /// zapíše si do zápisníku i situaci (u vody, u ohně...). Význam slova hra nikdy neprozradí.
    /// </summary>
    private void Say(PlayerSession speaker, (byte[] Word, Tone Tone) say)
    {
        var rules = GameData.Language;
        if (speaker.Character.IsDead || !Words.IsValid(say.Word) || !Enum.IsDefined(say.Tone)
            || _seconds - speaker.LastSaidAt < rules.SayCooldownSeconds)
        {
            return;
        }

        speaker.LastSaidAt = _seconds;
        var gesture = _seconds - speaker.LastGestureAt <= rules.Context.GestureSeconds ? speaker.LastGesture : null;
        var contexts = SpeechContexts.Of(Island, Fires, Animals, speaker.Character, speaker.X, speaker.Y, Clock.TotalMinutes, gesture);
        var speech = new SpeechView(speaker.Id, say.Word, say.Tone);
        foreach (var listener in _byPeer.Values)
        {
            var distance = Distance(speaker, listener);
            if (distance > rules.HearDistance)
            {
                continue;
            }

            SendTo(listener.Peer!, w => Messages.WriteSpeech(w, speech));
            if (listener != speaker && !listener.Character.IsDead && !listener.Character.Sleeping)
            {
                listener.Character.Vocabulary.Hear(say.Word, contexts, seesSpeaker: distance <= rules.ContextDistance);
            }
        }
    }

    /// <summary>Gesto vidí hráči do gestureDistance. Kývnutí a vrtění hlavou dají souvislost slovům řečeným hned potom.</summary>
    private void MakeGesture(PlayerSession player, (Gesture Gesture, float X, float Y) gesture)
    {
        if (player.Character.IsDead || !Enum.IsDefined(gesture.Gesture))
        {
            return;
        }

        player.LastGesture = gesture.Gesture;
        player.LastGestureAt = _seconds;
        var view = new GestureView(player.Id, gesture.Gesture, gesture.X, gesture.Y);
        foreach (var viewer in _byPeer.Values)
        {
            if (Distance(player, viewer) <= GameData.Language.GestureDistance)
            {
                SendTo(viewer.Peer!, w => Messages.WriteGestureSeen(w, view));
            }
        }
    }

    // ---------- kmeny a zásoby ----------

    /// <summary>
    /// Pozvání do kmene (návrh 14.1): okolí vidí gesto, pozvaný dostane pozvánku a odpoví.
    /// </summary>
    private void InviteToTribe(PlayerSession inviter, int inviteeId)
    {
        var invitee = _byPeer.Values.FirstOrDefault(p => p.Id == inviteeId);
        if (inviter.Character.IsDead || invitee is null || invitee == inviter)
        {
            return;
        }

        var result = Tribes.Invite(inviter.Id, inviter.X, inviter.Y, invitee.Id, invitee.X, invitee.Y, Clock.TotalMinutes);
        SendEvent(inviter, TribeEvent(result));
        if (result == TribeResult.Invited)
        {
            MakeGesture(inviter, (Gesture.Invite, (float)invitee.X, (float)invitee.Y));
            SendTo(invitee.Peer!, w => Messages.WriteTribeInvite(w, inviter.Id));
        }
    }

    private void AnswerInvite(PlayerSession invitee, (int InviterId, bool Accept) answer)
    {
        var result = Tribes.Answer(invitee.Id, answer.InviterId, answer.Accept, Clock.TotalMinutes);
        SendEvent(invitee, TribeEvent(result));
        var inviter = _byPeer.Values.FirstOrDefault(p => p.Id == answer.InviterId);
        if (inviter is not null && result is TribeResult.Joined or TribeResult.Declined)
        {
            SendEvent(inviter, TribeEvent(result == TribeResult.Joined ? TribeResult.TheyJoined : TribeResult.TheyDeclined));
        }
    }

    private void StockAction(PlayerSession player, Func<TribeResult> action)
    {
        if (!player.Character.IsDead)
        {
            SendEvent(player, TribeEvent(action()));
            SendInventory(player);
        }
    }

    private static GameEvent TribeEvent(TribeResult result)
    {
        return new GameEvent(EventKind.Tribe, (byte)result, default, []);
    }

    /// <summary>Pošle hráči jeho kmen a zásoby, když se od posledně něco změnilo.</summary>
    private void SendTribeState(PlayerSession player)
    {
        if (Tribes.Version != player.SentTribeVersion)
        {
            player.SentTribeVersion = Tribes.Version;
            var members = Tribes.TribeOf(player.Id) is null ? [] : Tribes.MembersWith(player.Id);
            SendTo(player.Peer!, w => Messages.WriteTribe(w, members));
        }

        var stockKey = $"{Island.Structures.Version}/{Island.Structures.StockVersion}/{Tribes.Version}/{_stockSpoilage}";
        if (stockKey != player.SentStockKey)
        {
            player.SentStockKey = stockKey;
            var stockpiles = Island.Structures.All
                .Where(s => s.Store is not null && Structures.MayTouch(s, player.Id, Tribes))
                .Select(s => new StockpileView(s.Id, new InventoryView(s.Store!.Capacity, s.Store.Stacks.Select(StackViewOf).ToArray())))
                .ToArray();
            SendTo(player.Peer!, w => Messages.WriteStockpiles(w, stockpiles));
        }
    }

    /// <summary>Které věci v zásobách jsou zkažené (když se něco zkazí, zásoby se pošlou znovu).</summary>
    private string StockSpoilage()
    {
        return string.Concat(Island.Structures.All.Where(s => s.Store is not null)
            .SelectMany(s => s.Store!.Stacks.Select(i => Spoilage.IsSpoiled(i.Item, Clock.TotalMinutes) ? '1' : '0')));
    }

    private void SendVocabulary(PlayerSession player)
    {
        var vocabulary = player.Character.Vocabulary;
        player.SentVocabularyVersion = vocabulary.Version;
        var entries = vocabulary.Entries
            .Select(e => new WordEntryView(e.Word, e.Heard, e.Contexts, e.Meaning, e.OldVoice))
            .ToArray();
        SendTo(player.Peer!, w => Messages.WriteVocabulary(w, new VocabularyView(entries, vocabulary.LastHeard ?? [])));
    }

    private static double Distance(PlayerSession a, PlayerSession b)
    {
        return Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
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
        return Island.Structures.All.Select(s => new StructureView(s.Id, s.Kind, (float)s.X, (float)s.Y, (float)s.Angle, (float)s.Length, s.Caught.HasValue ? (int)s.Caught.Value : -1, s.OwnerId, s.DecorationId)).ToArray();
    }

    private static DecorationTypeView[] DecorationTypeViews()
    {
        return GameData.Decorations.Select(d => new DecorationTypeView(d.Id, d.Name, d.Sticks, d.Stones, (float)d.Radius, d.Blocks)).ToArray();
    }

    private SignView[] SignViews()
    {
        return Island.Signs.All.Select(s => new SignView(
            s.Id, (float)s.X, (float)s.Y, s.Bits, s.AuthorId, s.CreatedAt, Signs.WeathersAt(s) ?? -1, s.Carved, s.Ancient,
            s.Record?.SpeciesId ?? -1, s.Record?.Name ?? [], s.Record?.Tags.ToArray() ?? [])).ToArray();
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
