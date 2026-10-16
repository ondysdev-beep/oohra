// Server Oohra. Spuštění: dotnet run --project Oohra.Server [cesta k server.json]
// Ukončení: Ctrl+C nebo příkaz konec (svět se uloží). Příkazy správce se píšou do okna serveru (pomoc).
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Oohra.Core.Data;
using Oohra.Server;

Console.OutputEncoding = Encoding.UTF8;

var configPath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "server.json");
var config = ServerConfig.Load(configPath);
// Soubory s nahlášeními a statistikami leží vedle server.json (když v něm není celá cesta).
var configDirectory = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? ".";
if (!Path.IsPathRooted(config.ReportsFile))
{
    config.ReportsFile = Path.Combine(configDirectory, config.ReportsFile);
}

if (config.StatsFile.Length > 0 && !Path.IsPathRooted(config.StatsFile))
{
    config.StatsFile = Path.Combine(configDirectory, config.StatsFile);
}

if (config.WorldFile.Length > 0 && !Path.IsPathRooted(config.WorldFile))
{
    config.WorldFile = Path.Combine(configDirectory, config.WorldFile);
}

using var server = new GameServer(config) { Log = message => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}") };
server.Start(config.Port);

Console.WriteLine($"Server Oohra běží na portu {server.Port}. Svět č. {config.Seed}. Ukonči Ctrl+C.");
Console.WriteLine(server.RestoredPlayers is { } restored
    ? $"Načten uložený svět z {config.WorldFile} (hráčů: {restored}, vznikl {server.WorldCreatedUtc.ToLocalTime():d. M. yyyy})."
    : config.WorldFile.Length > 0 ? $"Nový svět, bude se ukládat do {config.WorldFile}." : "Svět se neukládá (worldFile je prázdné).");
Console.WriteLine($"Nahlášení od hráčů se zapisují do {config.ReportsFile}.");
if (config.StatsFile.Length > 0)
{
    Console.WriteLine($"Statistiky hraní se ukládají do {config.StatsFile}.");
}

var running = true;
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    running = false;
};

// Příkazy správce: čtou se z okna serveru na vlastním vlákně, provádí je hlavní smyčka (mezi ticky).
var commands = new ConcurrentQueue<string>();
if (!Console.IsInputRedirected)
{
    Console.WriteLine(GameServer.CommandHelp + " (napiš do tohoto okna a Enter).");
    new Thread(() =>
    {
        while (Console.ReadLine() is { } line)
        {
            commands.Enqueue(line);
        }
    }) { IsBackground = true }.Start();
}

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
    while (commands.TryDequeue(out var command))
    {
        var answer = server.RunCommand(command);
        if (answer.Length > 0)
        {
            Console.WriteLine(answer);
        }

        running &= !server.StopRequested;
    }

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

Console.WriteLine("Server se vypíná, ukládám svět.");
