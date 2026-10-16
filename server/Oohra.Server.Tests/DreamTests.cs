namespace Oohra.Server.Tests;

/// <summary>Sen bez slov (návrh 4.2): nový hráč ho uvidí jen poprvé a server ho po dobu snu chrání.</summary>
public class DreamTests
{
    [Fact]
    public void OnlyFirstLoginDreams()
    {
        using var net = new TestNetwork();
        var first = net.Join("Ondra");
        Assert.True(first.NewCharacter);
        first.Dispose();
        Assert.True(net.Pump(() => net.Server.FindPlayer("Ondra") is { Online: false }));

        var again = net.Join("Ondra");

        Assert.True(again.LoggedIn);
        Assert.False(again.NewCharacter);
    }

    /// <summary>Ve snu postava nehladoví ani nežízní. Po probuzení už ano.</summary>
    [Fact]
    public void DreamerIsProtectedUntilAwake()
    {
        using var net = new TestNetwork(timeSpeed: 600, configure: c => c.DreamMaxSeconds = 90);
        var a = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        var thirst = player.Character.Hydration;

        net.PumpFor(300);
        Assert.Equal(thirst, player.Character.Hydration);

        a.DreamOver();
        Assert.True(net.Pump(() => player.Character.Hydration < thirst));
    }
}
