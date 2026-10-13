using LiteNetLib;
using LiteNetLib.Utils;
using Oohra.Core.Plants;

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

    /// <summary>Dlaždice se zdí (y * šířka + x).</summary>
    public int[] Walls { get; private set; } = [];

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

    public void Reborn()
    {
        Send(Messages.WriteReborn);
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

    /// <summary>Rozdělat oheň před sebou (klacky a kámen z batohu).</summary>
    public void LightFire()
    {
        Send(Messages.WriteLightFire);
    }

    /// <summary>Přiložit klacek do ohně (vyhaslý znovu zapálit).</summary>
    public void FeedFire(int fireId)
    {
        Send(w => Messages.WriteFeedFire(w, fireId));
    }

    /// <summary>Postavit zeď z klacků před sebou.</summary>
    public void BuildWall()
    {
        Send(Messages.WriteBuildWall);
    }

    /// <summary>Rozebrat zeď na dlaždici (vrátí se klacek).</summary>
    public void RemoveWall(int tileX, int tileY)
    {
        Send(w => Messages.WriteTileAction(w, MessageType.RemoveWall, tileX, tileY));
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
            case MessageType.Walls:
                Walls = Messages.ReadWalls(reader);
                break;
        }
    }
}
