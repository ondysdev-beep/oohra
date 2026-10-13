namespace Oohra.Core.Plants;

/// <summary>
/// Viditelné vlastnosti druhu rostliny: vzhled a kdy plodí. Tohle smí vidět klient.
/// Skryté složení je zvlášť v Secret.SpeciesSecret (stejný Id).
/// </summary>
public sealed class PlantSpecies
{
    public required int Id { get; init; }

    /// <summary>Barva listů (odstín 0 až 360, sytost a světlost v procentech).</summary>
    public required double Hue { get; init; }

    public required double Saturation { get; init; }

    public required double Lightness { get; init; }

    /// <summary>Odstín zralého plodu.</summary>
    public required int FruitHue { get; init; }

    /// <summary>Tvar rostliny 0 až 3 (jak ji kreslit).</summary>
    public required int Shape { get; init; }

    /// <summary>Měsíc v roce, kdy začíná plodit.</summary>
    public required int StartMonth { get; init; }
}
