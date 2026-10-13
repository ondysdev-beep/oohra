// Server Oohra. Spuštění: dotnet run --project Oohra.Server [cesta k server.json]
// Ukončení: Ctrl+C.
using System.Diagnostics;
using System.Text;
using Oohra.Core.Data;
using Oohra.Server;

Console.OutputEncoding = Encoding.UTF8;

var configPath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "server.json");
var config = ServerConfig.Load(configPath);
using var server = new GameServer(config) { Log = message => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}") };
server.Start(config.Port);

Console.WriteLine($"Server Oohra běží na portu {server.Port}. Svět č. {config.Seed}. Ukonči Ctrl+C.");

var running = true;
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    running = false;
};

// Hlavní smyčka: pevný počet ticků za sekundu (settings.json, 30).
// Každých StatsEverySeconds vypíše výkon (jak dlouho trvá tick, kolik dat odchází).
var tickSeconds = 1.0 / GameData.Settings.TicksPerSecond;
var stats = new ServerStats();
var clock = Stopwatch.StartNew();
var next = 0.0;
var nextReport = config.StatsEverySeconds;
while (running)
{
    var workStart = clock.Elapsed;
    server.Poll();
    var now = clock.Elapsed.TotalSeconds;
    var ticked = now >= next;
    if (ticked)
    {
        var tickStart = clock.Elapsed;
        server.Tick(tickSeconds);
        stats.AddTick((clock.Elapsed - tickStart).TotalMilliseconds);
        next += tickSeconds;
        if (now - next > 1)
        {
            // Server nestíhá (třeba po uspání počítače): nedoháníme, jen pokračujeme.
            next = now;
        }
    }

    stats.AddBusy((clock.Elapsed - workStart).TotalMilliseconds);
    if (config.StatsEverySeconds > 0 && now >= nextReport)
    {
        var report = stats.Report(now, server.OnlinePlayers, server.BytesSent, tickSeconds * 1000);
        if (server.OnlinePlayers > 0)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {report}");
        }

        nextReport = now + config.StatsEverySeconds;
    }

    if (!ticked)
    {
        Thread.Sleep(1);
    }
}

Console.WriteLine("Server se vypíná.");
