namespace Oohra.Core.Secret;

/// <summary>
/// Skryté složení jídla: šest látek. Jen na serveru, hráč vidí jen chuť a příznaky.
/// Pořadí v polích (double[6]) je vždy: výživa, dráždivost, toxin, nervový jed, povzbuzení, léčivo.
/// </summary>
public readonly record struct Composition(
    double Nutrition,
    double Irritant,
    double Toxin,
    double Neurotoxin,
    double Stimulant,
    double Medicine)
{
    public static Composition FromArray(double[] values)
    {
        return new Composition(values[0], values[1], values[2], values[3], values[4], values[5]);
    }
}
