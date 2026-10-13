using Newtonsoft.Json;
using Oohra.Core.Plants;
using Oohra.Core.World;

namespace Oohra.Core.Secret;

// SKRYTÁ DATA: vše ve složce Secret existuje jen na serveru a nikdy nesmí jít na klienta.

/// <summary>Archetyp rostliny: skryté složení a kde roste. Načítá se z Data/archetypes.json.</summary>
public sealed class Archetype
{
    public required IReadOnlyList<TileType> Home { get; init; }

    /// <summary>Králíci tuto rostlinu okusují.</summary>
    public bool Rabbit { get; init; }

    public required ArchetypeParts Parts { get; init; }

    /// <summary>Násobek složení po upečení.</summary>
    public required double[] Cook { get; init; }

    /// <summary>Dráždivost přidaná nezralému plodu. Když chybí, platí PlantRules.DefaultUnripeIrritant.</summary>
    public double? UnripeIrritant { get; init; }

    public SeasonalMultipliers? Seasonal { get; init; }

    [JsonIgnore]
    public bool HasFruit => Parts.Fruit is not null;

    public bool GrowsOn(TileType tile)
    {
        return Home.Contains(tile);
    }

    /// <summary>Základní složení části. Část, kterou archetyp nemá (plod), má samé nuly.</summary>
    public double[] BaseFor(PlantPart part)
    {
        var values = part switch
        {
            PlantPart.Leaf => Parts.Leaf,
            PlantPart.Root => Parts.Root,
            PlantPart.Fruit => Parts.Fruit,
            _ => throw new ArgumentOutOfRangeException(nameof(part)),
        };

        return values is null ? new double[6] : (double[])values.Clone();
    }

    /// <summary>Násobek podle ročního období, nebo null, když se část během roku nemění.</summary>
    public double[]? SeasonalFor(PlantPart part, Season season)
    {
        return Seasonal is not null && Seasonal.Part == part ? Seasonal.Multipliers[(int)season] : null;
    }
}

/// <summary>
/// Složení jednotlivých částí rostliny. Každá má 6 čísel:
/// výživa, dráždivost, toxin, nervový jed, povzbuzení, léčivo.
/// </summary>
public sealed class ArchetypeParts
{
    public required double[] Leaf { get; init; }

    public required double[] Root { get; init; }

    public double[]? Fruit { get; init; }
}

/// <summary>Násobky složení jedné části podle ročního období (jaro, léto, podzim, zima).</summary>
public sealed class SeasonalMultipliers
{
    public required PlantPart Part { get; init; }

    public required double[][] Multipliers { get; init; }
}
