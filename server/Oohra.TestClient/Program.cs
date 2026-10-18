// Konzolový klient pro zkoušení pravidel bez Unity.
// Spuštění: dotnet run --project Oohra.TestClient -- [seed] [velikost] [novy]
// Bez parametrů použije seed 1 a velikost ostrova z nastavení (settings.json).
// „novy“ = nový generátor světa s oblastmi (návrh 0.8), jinak starý ostrov z prototypu.
using System.Text;
using Oohra.Core.Data;
using Oohra.Core.Plants;
using Oohra.Core.World;
using Oohra.TestClient;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var seed = args.Length > 0 && int.TryParse(args[0], out var s) ? s : 1;
var size = args.Length > 1 && int.TryParse(args[1], out var z) ? z : GameData.Settings.WorldWidth;
var island = args.Contains("novy") ? WorldGenerator.Build(seed, size, size) : IslandGenerator.Build(seed, size, size);
var session = new PlayerSession(island);

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

    var words = line.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (words.Length == 0)
    {
        continue;
    }

    if (words[0] is "konec" or "q")
    {
        break;
    }

    Console.WriteLine(Commands.Run(words, island, session));
}

/// <summary>Příkazy konzole.</summary>
internal static class Commands
{
    public static string Run(string[] words, Island island, PlayerSession session)
    {
        switch (words[0])
        {
            case "mapa":
                return MapRenderer.Render(island) + MapRenderer.Legend;
            case "oblasti":
                return MapRenderer.RenderRegions(island) + MapRenderer.RegionLegend;
            case "druhy":
                return SpeciesPrinter.Render(island).TrimEnd();
            case "stav":
                return session.Status();
            case "plody":
                return session.FruitingSpecies();
            case "jez" or "jíst" or "sněz" or "snez":
                return Consume(words, session, taste: false);
            case "ochutnej":
                return Consume(words, session, taste: true);
            case "cekej" or "čekej":
                return Wait(words, session);
            case "generace" when words.Length > 1 && int.TryParse(words[1], out var g) && g >= 0:
                return session.SetGeneration(g);
            case "pomoc":
                return Help;
            default:
                return "Neznámý příkaz. Napiš „pomoc“.";
        }
    }

    private const string Help =
        "mapa                          textová mapa ostrova\n" +
        "oblasti                       zmenšená mapa oblastí a jejich podíly\n" +
        "druhy                         seznam druhů rostlin\n" +
        "plody                         které druhy teď plodí\n" +
        "jez <druh> <list|koren|plod> [upeceny|uvareny]   sní část rostliny\n" +
        "ochutnej <druh> <list|koren|plod> [upeceny|uvareny]   ochutná malý kousek\n" +
        "cekej <minuty>  nebo  cekej <hodiny>h   nechá běžet herní čas\n" +
        "stav                          stav těla a příznaky\n" +
        "generace <n>                  nová postava (0 = Pradávný, 1 a víc = Nový)\n" +
        "konec                         ukončí program\n" +
        "Pozn.: vaření zatím nepotřebuje oheň, je to jen pro zkoušení pravidel.";

    private static string Consume(string[] words, PlayerSession session, bool taste)
    {
        if (words.Length < 3 || !int.TryParse(words[1], out var species))
        {
            return "Použití: jez <druh> <list|koren|plod> [upeceny|uvareny]";
        }

        PlantPart? part = words[2] switch
        {
            "list" => PlantPart.Leaf,
            "koren" or "kořen" => PlantPart.Root,
            "plod" => PlantPart.Fruit,
            _ => null,
        };
        if (part is null)
        {
            return "Část musí být list, koren nebo plod.";
        }

        var cooking = words.Length > 3 ? words[3] switch
        {
            "upeceny" or "upečený" => Cooking.Baked,
            "uvareny" or "uvařený" => Cooking.Boiled,
            _ => Cooking.Raw,
        } : Cooking.Raw;

        return session.Consume(species, part.Value, cooking, taste);
    }

    private static string Wait(string[] words, PlayerSession session)
    {
        if (words.Length < 2)
        {
            return "Použití: cekej 30  nebo  cekej 5h";
        }

        var text = words[1];
        var hours = text.EndsWith('h');
        if (!double.TryParse(hours ? text[..^1] : text, out var amount) || amount <= 0)
        {
            return "Zadej počet minut (cekej 30) nebo hodin (cekej 5h).";
        }

        var minutes = hours ? amount * 60 : amount;
        var events = session.Wait(minutes).ToList();
        events.Add(session.Status());
        return string.Join("\n", events);
    }
}
