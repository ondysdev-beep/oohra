using Oohra.Core.Data;
using Oohra.Core.Social;

namespace Oohra.Core.World;

/// <summary>Chráněné místo narození: kdo se tu narodil, do kdy a v jakém okruhu.</summary>
public sealed record BirthGuard(double X, double Y, int PlayerId, double Until);

/// <summary>
/// Ochrana míst narození (návrh 4.1). Prvních 24 hodin po narození smí v okolí stavět jen ten, kdo se tam narodil,
/// a jeho kmen. Pak se okolí uvolní. Zabrání tomu, aby někdo zazdil nebo zastavěl cizí místo narození.
/// </summary>
public sealed class BirthGuards
{
    private readonly List<BirthGuard> _guards = [];

    public IReadOnlyList<BirthGuard> All => _guards;

    /// <summary>Vrátí ochrany z uloženého světa.</summary>
    public void Restore(IEnumerable<BirthGuard> saved)
    {
        _guards.AddRange(saved);
    }

    /// <summary>Hráč se narodil na (x, y). Jeho starší ochrana (z minulého života) končí.</summary>
    public void Add(double x, double y, int playerId, double now)
    {
        _guards.RemoveAll(g => g.PlayerId == playerId);
        _guards.Add(new BirthGuard(x, y, playerId, now + GameData.Building.BirthGuard.Hours * 60));
    }

    /// <summary>
    /// Brání cizí místo narození stavbě na (x, y)? Vlastní místo a místa kmene ne. Prošlé ochrany zahodí.
    /// </summary>
    public BirthGuard? Blocking(double x, double y, int builderId, Tribes tribes, double now)
    {
        _guards.RemoveAll(g => now >= g.Until);
        var radius = GameData.Building.BirthGuard.Radius;
        foreach (var guard in _guards)
        {
            var dx = guard.X - x;
            var dy = guard.Y - y;
            if (dx * dx + dy * dy < radius * radius && !tribes.SameTribe(guard.PlayerId, builderId))
            {
                return guard;
            }
        }

        return null;
    }
}
