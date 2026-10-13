// Zátěžový test: připojí roboty k běžícímu serveru a každých 5 sekund vypíše, jak se jim daří.
// Spuštění (server musí běžet): dotnet run --project Oohra.Bots [-count 20] [-server 127.0.0.1] [-port 9050] [-code OOHRA-TEST] [-seconds 60]
// Výkon serveru (délku ticku) vypisuje sám server ve svém okně.
using System.Diagnostics;
using System.Text;
using Oohra.Bots;
using Oohra.Protocol;

Console.OutputEncoding = Encoding.UTF8;

var count = int.Parse(Option("-count", "20"));
var host = Option("-server", "127.0.0.1");
var port = int.Parse(Option("-port", ProtocolInfo.DefaultPort.ToString()));
var code = Option("-code", "OOHRA-TEST");
var seconds = double.Parse(Option("-seconds", "60"));
const double ReportEverySeconds = 5;
const double JoinEverySeconds = 0.2;

Console.WriteLine($"Připojuji {count} robotů k {host}:{port} na {seconds:0} s.");
var bots = Enumerable.Range(1, count).Select(i => new Bot($"Bot{i:00}", i)).ToList();
var clock = Stopwatch.StartNew();
var joined = 0;
var nextReport = ReportEverySeconds;
var lastReport = 0.0;
var failed = false;

while (clock.Elapsed.TotalSeconds < seconds)
{
    var now = clock.Elapsed.TotalSeconds;

    // Roboti přicházejí postupně, jako lidé.
    if (joined < bots.Count && now >= joined * JoinEverySeconds)
    {
        bots[joined].Connection.Connect(host, port, code, bots[joined].Nickname);
        joined++;
    }

    foreach (var bot in bots.Take(joined))
    {
        bot.Update(now);
    }

    if (bots.FirstOrDefault(b => b.Connection.LoginError.Length > 0) is { } refused)
    {
        Console.WriteLine($"{refused.Nickname}: server odmítl přihlášení: {refused.Connection.LoginError}");
        failed = true;
        break;
    }

    if (now >= nextReport)
    {
        Report(now - lastReport);
        lastReport = now;
        nextReport += ReportEverySeconds;
    }

    Thread.Sleep(2);
}

var ready = bots.Count(b => b.Ready && !b.Connection.Disconnected);
Console.WriteLine();
Console.WriteLine($"Konec. Ve hře zůstalo {ready}/{count} robotů. " +
                  $"Celkem utrženo {bots.Sum(b => b.Harvests)}, snědeno {bots.Sum(b => b.Meals)}, " +
                  $"napito {bots.Sum(b => b.Drinks)}, úmrtí {bots.Sum(b => b.Deaths)}.");
foreach (var bot in bots)
{
    bot.Dispose();
}

return failed || ready < count ? 1 : 0;

// Řádek o stavu: kolik robotů je ve hře, kolik snímků za sekundu jim chodí (má být 15) a kolik hráčů vidí.
void Report(double elapsed)
{
    var active = bots.Where(b => b.Ready).ToList();
    var rates = active.Select(b => b.TakeSnapshotCount() / elapsed).ToList();
    var lost = bots.Count(b => b.Connection.Disconnected);
    var rateText = rates.Count > 0 ? $"snímků/s průměr {rates.Average():0.0}, nejméně {rates.Min():0.0}" : "zatím nic";
    var seen = active.Count > 0 ? active.Min(b => b.VisiblePlayers) : 0;
    Console.WriteLine($"[{clock.Elapsed.TotalSeconds,4:0} s] ve hře {active.Count}/{count}, odpojeno {lost}, {rateText}, " +
                      $"vidí nejméně {seen} hráčů, utrženo {bots.Sum(b => b.Harvests)}, snědeno {bots.Sum(b => b.Meals)}, " +
                      $"napito {bots.Sum(b => b.Drinks)}, úmrtí {bots.Sum(b => b.Deaths)}");
}

string Option(string name, string fallback)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
}
