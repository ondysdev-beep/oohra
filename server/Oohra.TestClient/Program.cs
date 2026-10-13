// Konzolový klient pro zkoušení pravidel bez Unity.
// Spuštění: dotnet run --project Oohra.TestClient -- [seed] [velikost]
// Bez parametrů použije seed 1 a velikost ostrova z nastavení (settings.json).
using System.Text;
using Oohra.Core.Data;
using Oohra.Core.World;
using Oohra.TestClient;

Console.OutputEncoding = Encoding.UTF8;

var seed = args.Length > 0 && int.TryParse(args[0], out var s) ? s : 1;
var size = args.Length > 1 && int.TryParse(args[1], out var z) ? z : GameData.Settings.WorldWidth;
var island = IslandGenerator.Build(seed, size, size);

Console.WriteLine($"Oohra TestClient · svět č. {seed} · ostrov {size} × {size}");
Console.WriteLine($"Rostlin {island.Plants.Count}, stromů {island.Trees.Count}. Napiš „pomoc“ pro seznam příkazů.");

while (true)
{
    Console.Write("> ");
    var line = Console.ReadLine();
    if (line is null)
    {
        break;
    }

    var command = line.Trim().ToLowerInvariant();
    if (command is "konec" or "q")
    {
        break;
    }

    switch (command)
    {
        case "mapa":
            Console.Write(MapRenderer.Render(island));
            Console.WriteLine(MapRenderer.Legend);
            break;
        case "druhy":
            Console.Write(SpeciesPrinter.Render(island));
            break;
        case "pomoc":
            Console.WriteLine("mapa   textová mapa ostrova");
            Console.WriteLine("druhy  seznam druhů rostlin");
            Console.WriteLine("konec  ukončí program");
            break;
        case "":
            break;
        default:
            Console.WriteLine("Neznámý příkaz. Napiš „pomoc“.");
            break;
    }
}
