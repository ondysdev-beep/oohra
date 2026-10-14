using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Ochrana místa narození přes síť (návrh 4.1).</summary>
public class BirthGuardServerTests
{
    /// <summary>U místa, kde se Petr právě narodil, Ondra stavět nesmí. Petr sám ano.</summary>
    [Fact]
    public void StrangerCannotWallInBirthPlace()
    {
        using var net = new TestNetwork(configure: c => c.StartSticks = 4);
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var petr = net.Server.FindPlayer("Petr")!;
        var ondra = net.Server.FindPlayer("Ondra")!;
        var (x, y) = (petr.X, petr.Y);
        ondra.X = x;
        ondra.Y = y;

        a.Place(BuildKind.Stockpile, (float)x + 1, (float)y, 0);
        Assert.True(net.Pump(() => a.TakeEvents().Any(e => e.Kind == EventKind.Build && e.Code == (byte)BuildResult.BirthPlaceProtected)));
        Assert.Empty(net.Server.Island.Structures.All);

        b.Place(BuildKind.Stockpile, (float)x + 1, (float)y, 0);
        Assert.True(net.Pump(() => b.TakeEvents().Any(e => e.Kind == EventKind.Build && e.Code == (byte)BuildResult.Built)));
        Assert.Single(net.Server.Island.Structures.All);
    }
}
