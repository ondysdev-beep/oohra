using System.Text;
using Oohra.Core.Data;
using Oohra.Core.World;

namespace Oohra.TestClient;

/// <summary>
/// Vypíše druhy rostlin tak, jak je vidí hráč: barva, tvar, kdy plodí a kolik jich na ostrově roste.
/// Složení (co je jedlé) záměrně neukazuje.
/// </summary>
public static class SpeciesPrinter
{
    private static readonly string[] _shapeNames = ["růžice", "stonek s lístky", "keřík", "trojlístek"];

    public static string Render(Island island)
    {
        var text = new StringBuilder();
        foreach (var species in island.Species)
        {
            var count = island.Plants.Count(p => p.SpeciesId == species.Id);
            var hasFruit = GameData.Archetypes[island.SpeciesSecrets[species.Id].ArchetypeIndex].HasFruit;
            var fruit = hasFruit
                ? $"plody {ColorName(species.FruitHue)} od {species.StartMonth + 1}. měsíce"
                : "bez plodů";

            text.Append($"Druh {species.Id}: ")
                .Append($"{ColorName(species.Hue)} listy ({species.Hue:0}°, sytost {species.Saturation:0} %, světlost {species.Lightness:0} %), ")
                .Append($"tvar {_shapeNames[species.Shape]}, {fruit}, na ostrově {count}×")
                .Append('\n');
        }

        return text.ToString();
    }

    /// <summary>Hrubý název barvy podle odstínu (0 až 360 stupňů).</summary>
    private static string ColorName(double hue)
    {
        var h = ((hue % 360) + 360) % 360;
        return h switch
        {
            < 15 => "červené",
            < 45 => "oranžové",
            < 70 => "žluté",
            < 160 => "zelené",
            < 200 => "tyrkysové",
            < 260 => "modré",
            < 320 => "fialové",
            < 345 => "růžové",
            _ => "červené",
        };
    }
}
