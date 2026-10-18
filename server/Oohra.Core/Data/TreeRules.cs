using Oohra.Core.World;

namespace Oohra.Core.Data;

/// <summary>Druhy stromů z Data/trees.json (návrh 0.8, kap. 2).</summary>
public sealed record TreeRules(int SeedSalt, List<TreeSpeciesRules> Species, int WaterDistance, double WillowNearWater)
{
    public TreeSpeciesRules For(TreeKind kind)
    {
        return Species.First(s => s.Kind == kind);
    }
}

/// <summary>Druh stromu: rozsah výšky (metry) a jak často roste v které oblasti.</summary>
public sealed record TreeSpeciesRules(TreeKind Kind, double MinHeight, double MaxHeight, Dictionary<RegionKind, double> Weights);
