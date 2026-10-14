using Oohra.Core.Data;

namespace Oohra.Core.Characters;

/// <summary>
/// Dovednosti postavy (prototyp S.skills, S.pend, practice, skillLvl a consolidate v part2.js).
/// Nácvik se během dne jen hromadí (čeká na spánek). Na body, a tedy úroveň, se převede až ve spánku.
/// Smrtí se dovednosti ztratí s celou postavou.
/// </summary>
public sealed class Skills
{
    private static readonly SkillKind[] All = [SkillKind.Gather, SkillKind.Hunt, SkillKind.Craft, SkillKind.Build];

    private readonly Dictionary<SkillKind, double> _points = [];
    private readonly Dictionary<SkillKind, double> _pending = [];

    /// <summary>Utvrzené body nácviku.</summary>
    public double Points(SkillKind skill)
    {
        return _points.GetValueOrDefault(skill);
    }

    /// <summary>Nácvik, který čeká na spánek.</summary>
    public double Pending(SkillKind skill)
    {
        return _pending.GetValueOrDefault(skill);
    }

    /// <summary>Úroveň 0 až maxLevel: odmocnina z bodů / levelDivisor, dolů.</summary>
    public int Level(SkillKind skill)
    {
        var rules = GameData.Skills;
        return Math.Min(rules.MaxLevel, (int)Math.Floor(Math.Sqrt(Points(skill) / rules.LevelDivisor)));
    }

    /// <summary>Procvičí dovednost. learnMultiplier: jak rychle se postava učí (Noví rychleji).</summary>
    public void Practice(SkillKind skill, double amount, double learnMultiplier)
    {
        _pending[skill] = Pending(skill) + amount * learnMultiplier;
    }

    /// <summary>
    /// Ve spánku převede nácvik na body. Vrací dovednosti, kterým stoupla úroveň (s novou úrovní).
    /// learned: jestli bylo co utvrdit.
    /// </summary>
    public List<(SkillKind Skill, int Level)> Consolidate(out bool learned)
    {
        learned = false;
        var levelUps = new List<(SkillKind, int)>();
        foreach (var skill in All)
        {
            var pending = Pending(skill);
            if (pending <= 0)
            {
                continue;
            }

            learned = true;
            var before = Level(skill);
            _points[skill] = Points(skill) + pending;
            _pending[skill] = 0;
            var after = Level(skill);
            if (after > before)
            {
                levelUps.Add((skill, after));
            }
        }

        return levelUps;
    }
}
