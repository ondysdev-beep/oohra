namespace Oohra.Core.Characters;

/// <summary>Dovednosti postavy (prototyp SKN v part1.js). Procvičují se během dne a utvrdí se ve spánku.</summary>
public enum SkillKind
{
    /// <summary>Sbírání: trhání a kopání rostlin.</summary>
    Gather = 0,

    /// <summary>Lov (přijde se zvířaty).</summary>
    Hunt = 1,

    /// <summary>Řemeslo: oheň, pečení, nástroje.</summary>
    Craft = 2,

    /// <summary>Stavění: zdi, zdi s otvorem, dveře.</summary>
    Build = 3,
}
