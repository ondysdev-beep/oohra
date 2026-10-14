using Oohra.Core.Data;
using Oohra.Core.Plants;
using Oohra.Core.Rng;
using Oohra.Core.Secret;

namespace Oohra.Core.World;

/// <summary>Stará rytina u ruiny: kde je, kresba a záznam.</summary>
public sealed record RuinCarving(double X, double Y, byte[] Bits, CarvingRecord Record);

/// <summary>
/// Staré rytiny s poznámkami, které se můžou mýlit (prototyp makeRuins a tagsFromComp v part5.js).
/// U každé ruiny je kresba jedné rostliny, její jméno ze slabik a co prý dělá. Ve 30 % je poznámka chybná.
/// </summary>
public static class RuinCarvings
{
    // Pořadí částí jako v prototypu (['fruit', 'root', 'leaf']), na něm závisí losování.
    private static readonly PlantPart[] PartOrder = [PlantPart.Fruit, PlantPart.Root, PlantPart.Leaf];

    public static List<RuinCarving> For(Island island)
    {
        var rules = GameData.Signs.Ruins;
        var r = new Mulberry32(island.Seed ^ rules.SeedSalt);
        var carvings = new List<RuinCarving>();
        foreach (var spot in island.RuinSpots)
        {
            var speciesId = r.NextInt(island.Species.Count);
            var secret = island.SpeciesSecrets[speciesId];
            var archetype = GameData.Archetypes[secret.ArchetypeIndex];
            var parts = PartOrder.Where(p => p != PlantPart.Fruit || archetype.HasFruit).ToArray();
            var part = parts[r.NextInt(parts.Length)];
            var item = new PlantItem(speciesId, part, Ripeness.Ripe, Season.Summer, TileType.Grass);
            var tags = TagsOf(CompositionCalculator.Compute(item, secret), rules);
            if (r.NextDouble() < rules.WrongChance)
            {
                if (tags.Count > 0 && r.NextDouble() < rules.DropChance)
                {
                    tags.RemoveAt(r.NextInt(tags.Count));
                }

                var extra = (CarvingTag)r.NextInt(10);
                if (!tags.Contains(extra))
                {
                    tags.Add(extra);
                }
            }

            byte[] name = [(byte)r.NextInt(10), (byte)r.NextInt(10)];
            var bits = ShapeBits(island.Species[speciesId].Shape);
            carvings.Add(new RuinCarving(spot.X, spot.Y, bits, new CarvingRecord(speciesId, name, tags)));
        }

        return carvings;
    }

    /// <summary>Co by o rostlině napsal ten, kdo ji jedl (prototyp tagsFromComp).</summary>
    public static List<CarvingTag> TagsOf(Composition v, RuinCarvingRules rules)
    {
        var tags = new List<CarvingTag>();
        if (v.Nutrition > rules.FillingAbove) tags.Add(CarvingTag.Filling);
        if (v.Toxin > rules.BitterAbove) tags.Add(CarvingTag.Bitter);
        if (v.Irritant > rules.BurnsAbove) tags.Add(CarvingTag.Burns);
        if (v.Irritant > rules.VomitAbove) tags.Add(CarvingTag.Vomit);
        if (v.Irritant > rules.DiarrheaAbove) tags.Add(CarvingTag.Diarrhea);
        if (v.Toxin > rules.FeverAbove) tags.Add(CarvingTag.Fever);
        if (v.Neurotoxin > rules.TremorAbove) tags.Add(CarvingTag.Tremor);
        if (v.Neurotoxin > rules.FaintAbove) tags.Add(CarvingTag.Faint);
        if (v.Stimulant > rules.StrengthenedAbove) tags.Add(CarvingTag.Strengthened);
        if (v.Medicine > rules.HealsAbove) tags.Add(CarvingTag.Heals);
        return tags;
    }

    /// <summary>Kresba tvaru rostliny 12 × 12 (prototyp bitsForShape): 0 květ, 1 lístek, 2 srdce, 3 kužel.</summary>
    public static byte[] ShapeBits(int shape)
    {
        var bits = SignBits.Empty();
        for (var y = 0; y < SignBits.Size; y++)
        {
            for (var x = 0; x < SignBits.Size; x++)
            {
                SignBits.Set(bits, x, y, ShapeHas(shape, x - 5.5, y - 5.5, y));
            }
        }

        return bits;
    }

    private static bool ShapeHas(int shape, double dx, double dy, int y)
    {
        var rr = Hypot(dx, dy);
        switch (shape)
        {
            case 0:
                var a = Math.Atan2(dy, dx);
                return rr < 1.5 || (rr < 5.3 && rr > 1.9 && Math.Cos(a * 4) > 0.15);
            case 1:
                return Math.Abs(dx + 0.5) < 0.8
                    || (y > 1 && y < 10 && Math.Abs(Math.Abs(dx + 0.5) - (10 - y) * 0.42) < 0.85 && rr < 6.2);
            case 2:
                return Hypot(dx + 2.3, dy + 1.4) < 2.6 || Hypot(dx - 2.3, dy + 1.4) < 2.6
                    || (dy > 0 && dy < 5.2 && Math.Abs(dx) < (5.4 - dy) * 0.8);
            default:
                return dy > -5 && dy < 5 && Math.Abs(dx) < (dy + 5.4) * 0.45;
        }
    }

    private static double Hypot(double x, double y)
    {
        return Math.Sqrt(x * x + y * y);
    }
}
