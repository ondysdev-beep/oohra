using Oohra.Core.Characters;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Spánek a dovednosti přes síť.</summary>
public class SleepServerTests
{
    [Fact]
    public void RestedPlayerCannotSleep()
    {
        using var net = new TestNetwork();
        var client = net.Join("Ondra");

        client.ToggleSleep();

        Assert.True(net.Pump(() => client.TakeEvents().Any(e => e.Kind == EventKind.Body && e.Code == (byte)BodyEvent.NotTired)));
        Assert.False(client.Self!.Sleeping);
    }

    [Fact]
    public void TiredPlayerSleepsOthersSeeItAndWalkingWakes()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var player = net.Server.FindPlayer("Ondra")!;
        player.Character.Energy = 40;

        a.ToggleSleep();

        Assert.True(net.Pump(() => a.Self!.Sleeping));
        Assert.True(net.Pump(() => b.Snapshot!.Players.Any(p => p.Id == a.PlayerId && p.Sleeping)));
        a.SendInput(1, 0);
        Assert.True(net.Pump(() => !a.Self!.Sleeping));
        Assert.Contains(a.TakeEvents(), e => e.Kind == EventKind.Body && e.Code == (byte)BodyEvent.WokeUp);
    }

    [Fact]
    public void PracticeSettlesInSleep()
    {
        using var net = new TestNetwork();
        var client = net.Join("Ondra");
        var c = net.Server.FindPlayer("Ondra")!.Character;
        c.Energy = 40;
        c.Practice(SkillKind.Gather, 2);

        Assert.True(net.Pump(() => (client.Self!.PendingSkills & 1) == 1));
        client.ToggleSleep();
        Assert.True(net.Pump(() => client.Self!.Sleeping));
        c.SleepMinutes = 61;
        client.ToggleSleep();

        Assert.True(net.Pump(() => !client.Self!.Sleeping && client.Self.SkillLevels[(int)SkillKind.Gather] == 1));
        Assert.Equal(0, client.Self!.PendingSkills);
        Assert.Contains(client.TakeEvents(), e => e.Code == (byte)BodyEvent.SkillLevelUp && e.Skill == (byte)SkillKind.Gather && e.Level == 1);
    }
}
