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
public sealed partial class GameServer : IDisposable
{
    private const int MinNicknameLength = 2;
    private const int MaxNicknameLength = 16;

    private readonly ServerConfig _config;
    private readonly NicknameFilter _nicknames;
    private readonly WorldView _worldView;
    private readonly NetManager _net;
    private readonly NetDataWriter _writer = new();
    private readonly Dictionary<int, PlayerSession> _byPeer = [];

    // Odpojení hráči, jejichž postava ještě chvíli zůstává ve světě (do kdy, sekundy serveru).
    private readonly Dictionary<PlayerSession, double> _lingering = [];
    private readonly Dictionary<string, PlayerSession> _byNickname = new(StringComparer.OrdinalIgnoreCase);
    private Mulberry32 _rng;
    private readonly PlantVisual[] _sentVisuals;
    private int _nextPlayerId = 1;
    private long _tick;
    private int _sentFiresVersion;
    private int _sentForestryVersion;
    private int _sentStructuresVersion;
    private int _sentSignsVersion;
    private int _sentRocksVersion;
    private int _sentWaspsVersion;
    private int _sentWaspsForestryVersion;
    private int _sentFloodVersion;
    private int _sentRacksVersion = -1;
    private int _sentCorpsesVersion;
    private readonly IReadOnlyDictionary<Concept, byte[]> _commonWords;
    private double _seconds;

    // Kdy se příště uloží statistiky hraní (sekundy od startu serveru).
    private double _nextStatsSaveAt;

    // Uložený svět (world.json) a kdy se příště uloží.
    private readonly WorldStore _worldStore;
    private double _nextWorldSaveAt;
    private string _stockSpoilage = string.Empty;

    public GameServer(ServerConfig config)
    {
        _config = config;
        var size = config.WorldSize > 0 ? config.WorldSize : GameData.Settings.WorldWidth;
        Island = WorldGenerator.BuildWorld(config.Seed, size, size);
        Clock = GameClock.FromSettings(config.StartHour * 60);
        if (config.StartMonth > 0)
        {
            Clock.Restore(config.StartHour * 60, config.StartMonth);
        }
        Plants = new PlantStates();
        _worldView = WorldViews.From(Island);
        _rng = new Mulberry32(config.Seed ^ 0x5E4E);
        _sentVisuals = new PlantVisual[Island.Plants.Count];
        _commonWords = CommonWords.For(config.Seed);
        Island.Signs.AddAncient(Island);
        Reports = new ReportLog(config.ReportsFile);
        _nicknames = NicknameFilter.Load(Path.IsPathRooted(config.BlockedWordsFile) ? config.BlockedWordsFile : Path.Combine(AppContext.BaseDirectory, config.BlockedWordsFile));
        PlayStats = new PlayStats(config.StatsFile);
        _nextStatsSaveAt = config.StatsSaveSeconds;
        Strangers = new Strangers(config.Seed, Island);
        Animals = new Animals(config.Seed) { Enabled = config.Animals };
        if (config.ForceWeather.Length > 0)
        {
            ForceWeather(config.ForceWeather);
        }

        if (config.Animals)
        {
            Animals.Populate(Island, []);
        }

        // Uložený svět: na čerstvý ostrov ze seedu se vrátí změny, hráči a čas.
        _worldStore = new WorldStore(config.WorldFile, config.BackupEveryMinutes, config.BackupsKept);
        _nextWorldSaveAt = config.WorldSaveSeconds;
        if (_worldStore.Load() is { } save)
        {
            RestoreWorld(save);
            RestoredPlayers = save.Players.Count;
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

    /// <summary>Kolik hráčů server načetl z uloženého světa (null = svět je nový).</summary>
    public int? RestoredPlayers { get; private set; }

    /// <summary>Soubor s uloženým světem (prázdné = neukládá se).</summary>
    public string WorldFile => _worldStore.Path;

    /// <summary>Kolik bajtů server celkem odeslal (pro měření zátěže).</summary>
    public long BytesSent => _net.Statistics.BytesSent;

    internal Island Island { get; }

    internal GameClock Clock { get; }

    internal PlantStates Plants { get; }

    internal Fires Fires { get; } = new();

    /// <summary>Maso v kouři ohňů a na sušácích.</summary>
    internal MeatRacks Racks { get; } = new();

    internal Materials Materials { get; } = new();

    internal Animals Animals { get; }

    /// <summary>Cizí kmeny (NPC). Když je server.json má vypnuté (strangers: false), stojí a nic neposílají.</summary>
    internal Strangers Strangers { get; }

    /// <summary>Kmeny hráčů (patří hráči, ne postavě, takže přežijí smrt).</summary>
    internal Tribes Tribes { get; } = new();

    /// <summary>Obsah deníků (stránky podle čísla deníku). Ukládá se se světem.</summary>
    public Journals Journals { get; } = new();

    /// <summary>Nahlášení od hráčů (tlačítko Nahlásit), zapisují se do souboru reportsFile.</summary>
    public ReportLog Reports { get; }

    /// <summary>Statistiky hraní pro ladění (ukládají se do souboru statsFile).</summary>
    public PlayStats PlayStats { get; }

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
        if (_seconds >= _nextStatsSaveAt)
        {
            _nextStatsSaveAt = _seconds + _config.StatsSaveSeconds;
            PlayStats.Save();
        }

        SaveWorldWhenDue();
        var minutes = Clock.Advance(realSeconds * _config.TimeSpeed);
        CurrentWeather = WeatherNow();
        Fires.Burn(minutes * Weather.BurnMultiplier(CurrentWeather));
        Animals.Update(Island, Fires, Plants, Clock, realSeconds, AnimalTargets());
        Island.Forestry.Update(Island, Animals, AnimalTargets(), realSeconds, Clock);
        Island.Flood.Update(Clock);
        Racks.Update(Fires, Island.Structures, Clock, CurrentWeather.Kind, minutes);
        Island.Tracks.Update(Island, AnimalTargets(), Animals.All, minutes, CurrentWeather.Kind);
        if (_config.Wasps)
        {
            Island.Wasps.Update(realSeconds, Clock.TotalMinutes, AnimalTargets());
        }
        UpdateStrangers(realSeconds);

        foreach (var player in InWorld.ToList())
        {
            Move(player, realSeconds);
            if (_seconds >= player.DreamUntil)
            {
                // V jeskyni neprší, nefouká a teplota je stálá.
                var inCave = Island.InCave(player.X, player.Y);
                var weather = inCave ? WeatherState.Calm : CurrentWeather;
                var sheltered = inCave || Shelter.IsSheltered(Island, player.X, player.Y);
                Physiology.Update(player.Character, Clock, minutes, Fires.WarmsAt(Island, player.X, player.Y), sheltered, weather, inCave ? GameData.Caves.Temperature : null);
                Torch.Burn(player.Character, Clock.TotalMinutes, minutes, weather);
                Caves.Discover(Island, player.Character, player.X, player.Y, Clock.TotalMinutes);
                Hazards.Update(player.Character, Island, Fires, player.X, player.Y, Clock, minutes, _rng);
            }

            if (player.Character.IsDead && !player.BodyLeft)
            {
                LeaveBody(player);
            }

            SendBodyEvents(player);
            if (player.Peer is null)
            {
                // Odpojený hráč, jehož postava ještě chvíli zůstává ve světě: počítá se, ale nic se mu neposílá.
                continue;
            }

            PlayStats.Online(player.Nickname, realSeconds, DateTime.UtcNow);
            if (player.Character.Vocabulary.Version != player.SentVocabularyVersion)
            {
                SendVocabulary(player);
            }

            SendRecipes(player);
            SendTracks(player);
            SendCaveEntrances(player);

            SendTribeState(player);
        }

        EndLingering();
        if (Fires.Version != _sentFiresVersion)
        {
            _sentFiresVersion = Fires.Version;
            Broadcast(w => Messages.WriteFires(w, FireViews()));
        }

        if (Island.Forestry.Version != _sentForestryVersion)
        {
            _sentForestryVersion = Island.Forestry.Version;
            Broadcast(w => Messages.WriteTreeStates(w, TreeStateViews()));
            Broadcast(w => Messages.WriteTimbers(w, TimberViews()));
        }

        Island.Corpses.Decay(Clock.TotalMinutes);
        if (Island.Corpses.Version != _sentCorpsesVersion)
        {
            _sentCorpsesVersion = Island.Corpses.Version;
            Broadcast(w => Messages.WriteCorpses(w, CorpseViews()));
        }

        if (Racks.Version != _sentRacksVersion)
        {
            _sentRacksVersion = Racks.Version;
            Broadcast(w => Messages.WriteRacks(w, RackViews()));
        }

        if (Island.Flood.Version != _sentFloodVersion)
        {
            _sentFloodVersion = Island.Flood.Version;
            Broadcast(w => Messages.WriteFloodedTiles(w, Island.Flood.FloodedTiles().ToList()));
        }

        // Hnízdo zmizí i s pokáceným stromem, proto se hlídá i verze kácení.
        if (Island.Wasps.Version != _sentWaspsVersion || Island.Forestry.Version != _sentWaspsForestryVersion)
        {
            _sentWaspsVersion = Island.Wasps.Version;
            _sentWaspsForestryVersion = Island.Forestry.Version;
            Broadcast(w => Messages.WriteWaspNests(w, WaspNestViews()));
        }

        Island.Rocks.Regrow(Clock.TotalMinutes);
        if (Island.Rocks.Version != _sentRocksVersion)
        {
            _sentRocksVersion = Island.Rocks.Version;
            Broadcast(w => Messages.WriteRockPiles(w, RockPileViews()));
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
            SendStrangers();
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
        PlayStats.Save();
        SaveWorld();
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
        if (_config.LogoutLingerSeconds > 0 && !player.Character.IsDead)
        {
            // Postava ještě chvíli zůstane ve světě, aby se odpojením nedalo utéct před vlkem.
            _lingering[player] = _seconds + _config.LogoutLingerSeconds;
            Log?.Invoke($"{player.Nickname} odešel (postava zůstane ve světě ještě {_config.LogoutLingerSeconds:0} s).");
            return;
        }

        Broadcast(w => Messages.WritePlayerLeft(w, player.Id));
        Log?.Invoke($"{player.Nickname} odešel.");
    }

    /// <summary>Hráči ve světě: připojení a odpojení, jejichž postava tu ještě chvíli zůstává.</summary>
    private IEnumerable<PlayerSession> InWorld => _byPeer.Values.Concat(_lingering.Keys);

    /// <summary>Postavám odpojených hráčů vypršel čas ve světě: zmizí (bezpečně uložené na místě).</summary>
    private void EndLingering()
    {
        foreach (var (player, until) in _lingering.ToList())
        {
            if (_seconds >= until)
            {
                _lingering.Remove(player);
                Broadcast(w => Messages.WritePlayerLeft(w, player.Id));
            }
        }
    }

    private void HandleLogin(NetPeer peer, NetDataReader reader)
    {
        var (version, inviteCode, rawNickname, playerKey) = Messages.ReadLogin(reader);
        var nickname = rawNickname.Trim();
        var error = LoginError(version, inviteCode, nickname, playerKey);
        if (error is not null)
        {
            SendTo(peer, w => Messages.WriteLoginResult(w, new LoginResult(false, error, -1)));
            return;
        }

        var isNew = FindPlayer(nickname) is null;
        var player = FindPlayer(nickname) ?? CreatePlayer(nickname);
        if (isNew)
        {
            // Sen bez slov (návrh 4.2): dokud se postava neprobudí, nehladoví a zvířata si jí nevšímají.
            player.DreamUntil = _seconds + _config.DreamMaxSeconds;
        }

        // Nová postava (nebo odemčená správcem) si zapamatuje klíč toho, kdo přišel.
        player.KeyHash ??= PlayerKeys.Hash(playerKey);
        player.Peer = peer;
        _lingering.Remove(player);
        _byPeer[peer.Id] = player;

        SendTo(peer, w => Messages.WriteLoginResult(w, new LoginResult(true, string.Empty, player.Id, isNew)));
        SendTo(peer, w => Messages.WriteWorld(w, _worldView));
        foreach (var other in InWorld)
        {
            SendTo(peer, w => Messages.WritePlayerJoined(w, InfoOf(other)));
        }

        Broadcast(w => Messages.WritePlayerJoined(w, InfoOf(player)), except: peer);
        SendTo(peer, w => Messages.WritePlantStates(w, AllPlantVisuals()));
        SendTo(peer, w => Messages.WriteFires(w, FireViews()));
        SendTo(peer, w => Messages.WriteTreeStates(w, TreeStateViews()));
        SendTo(peer, w => Messages.WriteTimbers(w, TimberViews()));
        SendTo(peer, w => Messages.WriteStructures(w, StructureViews()));
        SendTo(peer, w => Messages.WriteSigns(w, SignViews()));
        SendTo(peer, w => Messages.WriteDecorationTypes(w, DecorationTypeViews()));
        SendTo(peer, w => Messages.WriteRockPiles(w, RockPileViews()));
        SendTo(peer, w => Messages.WriteWaspNests(w, WaspNestViews()));
        SendTo(peer, w => Messages.WriteFloodedTiles(w, Island.Flood.FloodedTiles().ToList()));
        SendTo(peer, w => Messages.WriteRacks(w, RackViews()));
        player.SentTracks = (-1, 0, 0);
        SendTo(peer, w => Messages.WriteCorpses(w, CorpseViews()));
        SendSignature(player);
        player.SentTribeVersion = -1;
        player.SentStockKey = string.Empty;
        player.SentVocabularyVersion = -1;
        player.SentRecipes = null;
        SendInventory(player);
        SendSelfState(player);
        PlayStats.Joined(player.Nickname, DateTime.UtcNow);
        Log?.Invoke($"{player.Nickname} přišel (hráčů ve hře: {_byPeer.Count}).");
    }

    private string? LoginError(int version, string inviteCode, string nickname, string playerKey)
    {
        if (version != ProtocolInfo.Version)
        {
            return "Máš jinou verzi hry než server.";
        }

        if (!_config.InviteCodes.Contains(inviteCode.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return "Neplatný kód pozvánky.";
        }

        if (IsBanned(nickname))
        {
            return "Správce tě z testu vyloučil.";
        }

        if (nickname.Length < MinNicknameLength || nickname.Length > MaxNicknameLength
            || !nickname.All(ch => char.IsLetterOrDigit(ch) || ch is ' ' or '-' or '_'))
        {
            return $"Přezdívka musí mít {MinNicknameLength} až {MaxNicknameLength} písmen nebo číslic.";
        }

        if (_nicknames.IsBlocked(nickname))
        {
            return "Tahle přezdívka nejde, obsahuje sprosté nebo urážlivé slovo. Vyber si jinou.";
        }

        if (FindPlayer(nickname) is { } existing && !PlayerKeys.Matches(existing.KeyHash, playerKey))
        {
            return "Tuhle přezdívku už má jiný hráč. Vyber si jinou.";
        }

        if (FindPlayer(nickname) is { Online: true })
        {
            return "Hráč s touto přezdívkou už je ve hře.";
        }

        return null;
    }

    /// <summary>Nový hráč: narodí se jako generace světa (prvních půl roku Pradávný) v divočině u vody.</summary>
    private PlayerSession CreatePlayer(string nickname)
    {
        // První život: aktuální generace světa (prvních půl roku Pradávní, kdo přijde později, je už z mladší generace).
        var character = NewCharacter(WorldGeneration);
        character.BornAsNewcomer(Clock.TotalMinutes);
        var spawn = SpawnPoint.PickWild(Island, _rng, firstLife: true);
        var player = new PlayerSession(_nextPlayerId++, nickname, character, NewInventory(character), spawn.X, spawn.Y);
        _byNickname[nickname] = player;
        Island.BirthGuards.Add(spawn.X, spawn.Y, player.Id, Clock.TotalMinutes);
        player.BornAt = Clock.TotalMinutes;
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
        if (_config.StartBag)
        {
            inventory.TryAdd(new Item(ItemKind.SmallBag));
        }

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

    // ---------- simulace ----------

    private void Move(PlayerSession player, double realSeconds)
    {
        var c = player.Character;
        if (c.IsDead || c.Sleeping || c.FaintMinutes > 0 || c.StunSeconds > 0 || (player.InputX == 0 && player.InputY == 0))
        {
            return;
        }

        var distance = Movement.Distance(Island, c, player.X, player.Y, realSeconds) * Movement.CarryMultiplier(player.Inventory);
        var (x, y) = Movement.Step(Island, player.X, player.Y, player.InputX, player.InputY, distance);
        player.X = x;
        player.Y = y;
        player.Facing = Math.Atan2(player.InputY, player.InputX);
    }
}
