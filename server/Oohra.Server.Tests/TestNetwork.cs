using System.Diagnostics;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>
/// Spustí skutečný server na volném portu a připojí k němu klienty přes síť (localhost).
/// Pump točí server i klienty, dokud neplatí podmínka.
/// </summary>
public sealed class TestNetwork : IDisposable
{
    public const string InviteCode = "TEST-KOD";

    private readonly List<GameConnection> _clients = [];

    public TestNetwork(double timeSpeed = 1, Action<ServerConfig>? configure = null)
    {
        var config = new ServerConfig
        {
            Port = 0,
            Seed = 12345,
            WorldSize = 84,
            InviteCodes = [InviteCode],
            TimeSpeed = timeSpeed,
            Animals = false,
            // Statistiky se v testech jen počítají, do souboru se neukládají.
            StatsFile = string.Empty,
        };
        configure?.Invoke(config);
        Server = new GameServer(config);
        Server.Start(0);
    }

    public GameServer Server { get; }

    /// <summary>Jak dlouho trvaly ticky serveru (ms), pro zátěžové testy.</summary>
    public List<double> TickMilliseconds { get; } = [];

    /// <summary>Připojí klienta a počká na výsledek přihlášení.</summary>
    public GameConnection Join(string nickname, string inviteCode = InviteCode)
    {
        var client = new GameConnection();
        _clients.Add(client);
        client.Connect("127.0.0.1", Server.Port, inviteCode, nickname);
        Pump(() => client.LoggedIn && client.World is not null && client.Self is not null || client.LoginError.Length > 0);
        return client;
    }

    /// <summary>Točí server (30 ticků za sekundu herního času) a klienty, dokud podmínka neplatí. Vrací, zda platí.</summary>
    public bool Pump(Func<bool> until, int timeoutMs = 5000)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < timeoutMs)
        {
            Server.Poll();
            var tickStart = watch.Elapsed;
            Server.Tick(1.0 / 30);
            TickMilliseconds.Add((watch.Elapsed - tickStart).TotalMilliseconds);
            foreach (var client in _clients)
            {
                client.Poll();
            }

            if (until())
            {
                return true;
            }

            Thread.Sleep(1);
        }

        return false;
    }

    /// <summary>Chvíli nechá vše běžet (aby došly zprávy).</summary>
    public void PumpFor(int milliseconds)
    {
        Pump(() => false, milliseconds);
    }

    public void Dispose()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        Server.Dispose();
    }
}
