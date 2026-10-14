using LiteNetLib;
using LiteNetLib.Utils;
using Oohra.Core.Language;
using Oohra.Core.Plants;
using Oohra.Core.Social;
using Oohra.Core.World;

namespace Oohra.Protocol;

/// <summary>
/// Síťový klient: připojí se k serveru, přihlásí se a drží poslední stav, který server poslal
/// (mapa, hráči, vlastní tělo, batoh, hlášky). Volej Poll() často (každý snímek), jinak nic nepřijde.
/// Klient posílá jen úmysly (jdi, utrhni, sněz). Co se opravdu stane, rozhoduje server.
/// </summary>
public sealed class GameConnection : IDisposable
{
    private readonly NetManager _net;
    private readonly NetDataWriter _writer = new();
    private readonly Dictionary<int, PlayerInfo> _players = [];
    private readonly Queue<GameEvent> _events = new();
    private readonly Queue<SpeechView> _speech = new();
    private readonly Queue<GestureView> _gestures = new();
    private readonly Queue<int> _invites = new();
    private NetPeer? _server;
    private string _inviteCode = string.Empty;
    private string _nickname = string.Empty;
    private (float X, float Y) _lastInput;

    public GameConnection()
    {
        var listener = new EventBasedNetListener();
        listener.PeerConnectedEvent += OnConnected;
        listener.PeerDisconnectedEvent += (peer, info) => Disconnected = true;
        listener.NetworkReceiveEvent += (peer, reader, channel, method) => OnReceive(reader);
        _net = new NetManager(listener) { AutoRecycle = true };
        _net.Start();
    }

    public bool LoggedIn { get; private set; }

    public bool Disconnected { get; private set; }

    /// <summary>Důvod, proč server přihlášení odmítl (nebo prázdný text).</summary>
    public string LoginError { get; private set; } = string.Empty;

    public int PlayerId { get; private set; } = -1;

    public WorldView? World { get; private set; }

    public Snapshot? Snapshot { get; private set; }

    public SelfState? Self { get; private set; }

    public InventoryView Inventory { get; private set; } = new(0, []);

    /// <summary>Vzhled každé rostliny (plody, vykopaná). Index = číslo rostliny.</summary>
    public PlantVisual[] PlantVisuals { get; private set; } = [];

    /// <summary>Ohně na ostrově (i vyhaslé, z nich zbyl popel).</summary>
    public FireView[] Fires { get; private set; } = [];

    /// <summary>Stavby hráčů (zdi).</summary>
    public StructureView[] Structures { get; private set; } = [];

    /// <summary>Zvířata v okolí (server posílá jen blízká).</summary>
    public AnimalView[] Animals { get; private set; } = [];

    /// <summary>Zápisník slov (ztratí se smrtí, server pošle nový).</summary>
    public VocabularyView Vocabulary { get; private set; } = new([], []);

    /// <summary>Značky a rytiny ve světě.</summary>
    public SignView[] Signs { get; private set; } = [];

    /// <summary>Čísla hráčů v mém kmeni (včetně mě). Prázdné = nejsem v kmeni.</summary>
    public int[] TribeMembers { get; private set; } = [];

    /// <summary>Zásoby kmene, do kterých smím (moje a mého kmene).</summary>
    public StockpileView[] Stockpiles { get; private set; } = [];

    /// <summary>Dekorace, které jde postavit (ze serveru).</summary>
    public DecorationTypeView[] DecorationTypes { get; private set; } = [];

    /// <summary>Hromady kamenů na skalách (i prázdné, Stones 0).</summary>
    public RockPileView[] RockPiles { get; private set; } = [];

    /// <summary>Těla mrtvých hráčů (i s věcmi).</summary>
    public CorpseView[] Corpses { get; private set; } = [];

    /// <summary>Můj podpis: jméno ze slabik (prázdné = ještě žádný) a znak.</summary>
    public byte[] SignatureName { get; private set; } = [];

    public byte[] SignatureMark { get; private set; } = [];

    public IReadOnlyDictionary<int, PlayerInfo> Players => _players;

    public void Connect(string host, int port, string inviteCode, string nickname)
    {
        _inviteCode = inviteCode;
        _nickname = nickname;
        _net.Connect(host, port, ProtocolInfo.ConnectionKey);
    }

    /// <summary>Zpracuje přijaté zprávy. Volej každý snímek.</summary>
    public void Poll()
    {
        _net.PollEvents();
    }

    /// <summary>Vrátí nové hlášky od serveru a smaže je.</summary>
    public List<GameEvent> TakeEvents()
    {
        var events = _events.ToList();
        _events.Clear();
        return events;
    }

    /// <summary>Směr chůze (x doprava, y dolů na mapě). Posílá se jen, když se změní.</summary>
    public void SendInput(float dirX, float dirY)
    {
        if (_lastInput == (dirX, dirY))
        {
            return;
        }

        _lastInput = (dirX, dirY);
        Send(w => Messages.WriteInput(w, dirX, dirY));
    }

    public void Harvest(int plantId, PlantPart part)
    {
        Send(w => Messages.WriteHarvest(w, plantId, part));
    }

    public void Drink(int tileX, int tileY)
    {
        Send(w => Messages.WriteDrink(w, tileX, tileY));
    }

    public void Eat(int stackIndex)
    {
        Send(w => Messages.WriteStackAction(w, MessageType.Eat, stackIndex));
    }

    public void Taste(int stackIndex)
    {
        Send(w => Messages.WriteStackAction(w, MessageType.Taste, stackIndex));
    }

    public void Drop(int stackIndex)
    {
        Send(w => Messages.WriteStackAction(w, MessageType.Drop, stackIndex));
    }

    /// <summary>Lehnout si ke spánku, nebo vstát. Jestli postava usne, rozhodne server (únava, teplo).</summary>
    public void ToggleSleep()
    {
        Send(Messages.WriteSleep);
    }

    /// <summary>Zaútočit na zvíře (pěstí, nebo kopím, když ho má v batohu).</summary>
    public void Attack(int animalId)
    {
        Send(w => Messages.WriteAnimalAction(w, MessageType.Attack, animalId));
    }

    /// <summary>Stáhnout maso z mrtvého zvířete.</summary>
    public void Butcher(int animalId)
    {
        Send(w => Messages.WriteAnimalAction(w, MessageType.Butcher, animalId));
    }

    /// <summary>Vyrobit kopí (klacek a kámen).</summary>
    /// <summary>Rybařit s kopím na dlaždici s vodou (jezírko, moře).</summary>
    public void Fish(int tileX, int tileY)
    {
        Send(w => Messages.WriteTileAction(w, MessageType.Fish, tileX, tileY));
    }

    /// <summary>Vzít úlovek z pasti (číslo stavby).</summary>
    public void TakeCatch(int trapId)
    {
        Send(w => Messages.WriteAnimalAction(w, MessageType.TakeCatch, trapId));
    }

    public void MakeSpear()
    {
        Send(Messages.WriteMakeSpear);
    }

    /// <summary>Narodit se znovu (po smrti). U kmene jen když hráč v nějakém je, jinak server zvolí divočinu.</summary>
    public void Reborn(BirthPlace place = BirthPlace.Wild)
    {
        Send(w => Messages.WriteReborn(w, place));
    }

    /// <summary>Nasbírat klacky u stromu na dlaždici.</summary>
    public void GatherSticks(int tileX, int tileY)
    {
        Send(w => Messages.WriteTileAction(w, MessageType.GatherSticks, tileX, tileY));
    }

    /// <summary>Vzít kámen ze skály na dlaždici.</summary>
    public void TakeStone(int tileX, int tileY)
    {
        Send(w => Messages.WriteTileAction(w, MessageType.TakeStone, tileX, tileY));
    }

    /// <summary>Přiložit klacek do ohně (vyhaslý znovu zapálit).</summary>
    public void FeedFire(int fireId)
    {
        Send(w => Messages.WriteFeedFire(w, fireId));
    }

    /// <summary>Režim stavby: postavit stavbu se středem (x, y), úhlem v radiánech a délkou zdi v metrech.</summary>
    public void Place(BuildKind kind, float x, float y, float angle, float length = 1f, string decoration = "")
    {
        Send(w => Messages.WritePlace(w, kind, x, y, angle, length, decoration));
    }

    /// <summary>Rozebrat stavbu (u zdi se vrátí klacek).</summary>
    public void RemoveStructure(int structureId)
    {
        Send(w => Messages.WriteRemoveStructure(w, structureId));
    }

    /// <summary>Říct slovo (1 až 3 slabiky, čísla 0 až 9) s náladou.</summary>
    public void Say(byte[] word, Tone tone = Tone.Calm)
    {
        Send(w => Messages.WriteSay(w, word, tone));
    }

    /// <summary>Udělat gesto. U ukázání je (x, y) místo na mapě.</summary>
    public void MakeGesture(Gesture gesture, float x = 0, float y = 0)
    {
        Send(w => Messages.WriteGesture(w, gesture, x, y));
    }

    /// <summary>Zapsat si význam slova do zápisníku (null = smazat).</summary>
    public void SetMeaning(byte[] word, Concept? meaning)
    {
        Send(w => Messages.WriteSetMeaning(w, word, meaning));
    }

    /// <summary>Nakreslit značku (kresba SignBits) na místo (x, y) kousek od postavy. Na skále to bude rytina.</summary>
    public void DrawSign(float x, float y, byte[] bits)
    {
        Send(w => Messages.WriteDrawSign(w, x, y, bits));
    }

    /// <summary>Smazat vlastní značku.</summary>
    public void EraseSign(int signId)
    {
        Send(w => Messages.WriteEraseSign(w, signId));
    }

    /// <summary>Pozvat hráče do kmene (musí stát kousek od tebe).</summary>
    public void InviteToTribe(int playerId)
    {
        Send(w => Messages.WriteInviteToTribe(w, playerId));
    }

    /// <summary>Přijmout nebo odmítnout pozvánku hráče inviterId.</summary>
    public void AnswerInvite(int inviterId, bool accept)
    {
        Send(w => Messages.WriteAnswerInvite(w, inviterId, accept));
    }

    public void LeaveTribe()
    {
        Send(Messages.WriteLeaveTribe);
    }

    /// <summary>Uložit hromádku z batohu do zásob kmene.</summary>
    public void StoreItem(int stockpileId, int stackIndex)
    {
        Send(w => Messages.WriteStockAction(w, MessageType.StoreItem, stockpileId, stackIndex));
    }

    /// <summary>Vzít hromádku ze zásob kmene do batohu.</summary>
    public void TakeItem(int stockpileId, int stackIndex)
    {
        Send(w => Messages.WriteStockAction(w, MessageType.TakeItem, stockpileId, stackIndex));
    }

    /// <summary>Vzít z těla celou hromádku do batohu.</summary>
    public void TakeFromCorpse(int corpseId, int stackIndex)
    {
        Send(w => Messages.WriteStockAction(w, MessageType.TakeFromCorpse, corpseId, stackIndex));
    }

    /// <summary>Nastavit si podpis (jméno ze slabik a znak).</summary>
    public void SetSignature(byte[] name, byte[] mark)
    {
        Send(w => Messages.WriteSetSignature(w, name, mark));
    }

    /// <summary>Nahlásit hráče, značku nebo obecný problém správci (tlačítko Nahlásit). Odpověď přijde jako hláška.</summary>
    public void Report(ReportTarget target, int targetId, ReportReason reason)
    {
        Send(w => Messages.WriteReport(w, target, targetId, reason));
    }

    /// <summary>Kdo tě od posledního volání pozval do kmene (čísla hráčů).</summary>
    public List<int> TakeInvites()
    {
        var invites = _invites.ToList();
        _invites.Clear();
        return invites;
    }

    /// <summary>Slova, která od posledního volání zazněla poblíž (i vlastní).</summary>
    public List<SpeechView> TakeSpeech()
    {
        var speech = _speech.ToList();
        _speech.Clear();
        return speech;
    }

    /// <summary>Gesta, která od posledního volání někdo poblíž udělal (i vlastní).</summary>
    public List<GestureView> TakeGestures()
    {
        var gestures = _gestures.ToList();
        _gestures.Clear();
        return gestures;
    }

    /// <summary>Upéct jeden kus z hromádky u ohně.</summary>
    public void Bake(int stackIndex)
    {
        Send(w => Messages.WriteStackAction(w, MessageType.Bake, stackIndex));
    }

    /// <summary>Vlastní poloha podle posledního snímku od serveru.</summary>
    public PlayerSnapshot? SelfSnapshot()
    {
        if (Snapshot is null)
        {
            return null;
        }

        foreach (var player in Snapshot.Players)
        {
            if (player.Id == PlayerId)
            {
                return player;
            }
        }

        return null;
    }

    public void Dispose()
    {
        _net.Stop();
    }

    private void OnConnected(NetPeer peer)
    {
        _server = peer;
        Send(w => Messages.WriteLogin(w, _inviteCode, _nickname));
    }

    private void Send(Action<NetDataWriter> write)
    {
        if (_server is null)
        {
            return;
        }

        _writer.Reset();
        write(_writer);
        _server.Send(_writer, DeliveryMethod.ReliableOrdered);
    }

    private void OnReceive(NetDataReader reader)
    {
        var type = (MessageType)reader.GetByte();
        switch (type)
        {
            case MessageType.LoginResult:
                var result = Messages.ReadLoginResult(reader);
                LoggedIn = result.Ok;
                LoginError = result.Reason;
                PlayerId = result.PlayerId;
                break;
            case MessageType.World:
                World = Messages.ReadWorld(reader);
                PlantVisuals = new PlantVisual[World.Plants.Length];
                break;
            case MessageType.Snapshot:
                Snapshot = Messages.ReadSnapshot(reader);
                break;
            case MessageType.PlayerJoined:
                var info = Messages.ReadPlayerJoined(reader);
                _players[info.Id] = info;
                break;
            case MessageType.PlayerLeft:
                _players.Remove(Messages.ReadPlayerLeft(reader));
                break;
            case MessageType.SelfState:
                Self = Messages.ReadSelfState(reader);
                break;
            case MessageType.Inventory:
                Inventory = Messages.ReadInventory(reader);
                break;
            case MessageType.Event:
                _events.Enqueue(Messages.ReadEvent(reader));
                break;
            case MessageType.PlantStates:
                foreach (var change in Messages.ReadPlantStates(reader))
                {
                    if (change.PlantId >= 0 && change.PlantId < PlantVisuals.Length)
                    {
                        PlantVisuals[change.PlantId] = change.Visual;
                    }
                }

                break;
            case MessageType.Fires:
                Fires = Messages.ReadFires(reader);
                break;
            case MessageType.Animals:
                Animals = Messages.ReadAnimals(reader);
                break;
            case MessageType.Structures:
                Structures = Messages.ReadStructures(reader);
                break;
            case MessageType.Speech:
                _speech.Enqueue(Messages.ReadSpeech(reader));
                break;
            case MessageType.GestureSeen:
                _gestures.Enqueue(Messages.ReadGestureSeen(reader));
                break;
            case MessageType.Vocabulary:
                Vocabulary = Messages.ReadVocabulary(reader);
                break;
            case MessageType.Signs:
                Signs = Messages.ReadSigns(reader);
                break;
            case MessageType.TribeInvite:
                _invites.Enqueue(Messages.ReadTribeInvite(reader));
                break;
            case MessageType.Tribe:
                TribeMembers = Messages.ReadTribe(reader);
                break;
            case MessageType.Stockpiles:
                Stockpiles = Messages.ReadStockpiles(reader);
                break;
            case MessageType.DecorationTypes:
                DecorationTypes = Messages.ReadDecorationTypes(reader);
                break;
            case MessageType.RockPiles:
                RockPiles = Messages.ReadRockPiles(reader);
                break;
            case MessageType.Corpses:
                Corpses = Messages.ReadCorpses(reader);
                break;
            case MessageType.Signature:
                (SignatureName, SignatureMark) = Messages.ReadSignature(reader);
                break;
        }
    }
}
