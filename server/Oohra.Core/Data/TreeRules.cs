using Oohra.Core.World;

namespace Oohra.Core.Data;

/// <summary>Druhy stromů z Data/trees.json (návrh 0.8, kap. 2).</summary>
public sealed record TreeRules(int SeedSalt, List<TreeSpeciesRules> Species, int WaterDistance, double WillowNearWater, RegrowthRules Regrowth)
{
    public TreeSpeciesRules For(TreeKind kind)
    {
        return Species.First(s => s.Kind == kind);
    }
}

/// <summary>Obnova lesa: kde a jak rychle roste mladý strom za pokácený (stages = kolikrát za růst se pošle hráčům).</summary>
public sealed record RegrowthRules(double MinDistance, double MaxDistance, double GrowYears, double BlocksFrom, double StructureClearance, int Stages);

/// <summary>Druh stromu: rozsah výšky (metry), rozestup od sousedů, kolik seknutí vydrží, kolik dřeva dá klad a kde roste.</summary>
public sealed record TreeSpeciesRules(TreeKind Kind, double MinHeight, double MaxHeight, double Spacing, int Hits, int WoodPerPiece, Dictionary<RegionKind, double> Weights);
