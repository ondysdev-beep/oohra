using Oohra.Core.Data;
using Oohra.Core.Social;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Místo narození je 24 hodin chráněné před cizí stavbou (návrh 4.1).</summary>
public class BirthGuardTests
{
    [Fact]
    public void StrangerCannotBuildAtFreshBirthPlace()
    {
        var guards = new BirthGuards();
        var tribes = new Tribes();
        guards.Add(10, 10, playerId: 1, now: 0);

        Assert.NotNull(guards.Blocking(11, 10, builderId: 2, tribes, now: 60));
        Assert.Null(guards.Blocking(11, 10, builderId: 1, tribes, now: 60));
        Assert.Null(guards.Blocking(10 + GameData.Building.BirthGuard.Radius + 0.1, 10, builderId: 2, tribes, now: 60));
    }

    [Fact]
    public void GuardEndsAfterHours()
    {
        var guards = new BirthGuards();
        guards.Add(10, 10, playerId: 1, now: 0);
        var end = GameData.Building.BirthGuard.Hours * 60;

        Assert.NotNull(guards.Blocking(10, 10, 2, new Tribes(), end - 1));
        Assert.Null(guards.Blocking(10, 10, 2, new Tribes(), end));
        Assert.Empty(guards.All);
    }

    [Fact]
    public void TribeMayBuildAtMembersBirthPlace()
    {
        var guards = new BirthGuards();
        var tribes = new Tribes();
        Assert.Equal(TribeResult.Invited, tribes.Invite(1, 0, 0, 2, 0, 0, 0));
        Assert.Equal(TribeResult.Joined, tribes.Answer(2, 1, accept: true, 0));
        guards.Add(10, 10, playerId: 1, now: 0);

        Assert.Null(guards.Blocking(10, 10, builderId: 2, tribes, now: 0));
    }

    /// <summary>Nové narození přenese ochranu na nové místo (staré se uvolní).</summary>
    [Fact]
    public void NewBirthMovesGuard()
    {
        var guards = new BirthGuards();
        guards.Add(10, 10, playerId: 1, now: 0);
        guards.Add(40, 40, playerId: 1, now: 100);

        Assert.Single(guards.All);
        Assert.Null(guards.Blocking(10, 10, 2, new Tribes(), 120));
        Assert.NotNull(guards.Blocking(40, 40, 2, new Tribes(), 120));
    }
}
