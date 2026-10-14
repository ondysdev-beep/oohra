using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Pradávní a Noví přes síť (návrh 8).</summary>
public class GenerationServerTests
{
    /// <summary>Generace patří světu (8.2): kdo umře dvakrát po sobě, je pořád Nový generace světa, ne o dvě výš.</summary>
    [Fact]
    public void DyingTwiceDoesNotFarmGenerations()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        Assert.True(net.Pump(() => a.Self is { Generation: 0, WorldGeneration: 1 }));

        for (var life = 0; life < 2; life++)
        {
            Kill(net, a, "Ondra");
            a.Reborn();
            Assert.True(net.Pump(() => a.Self is { Dead: false, Generation: 1 }));
        }

        Assert.Equal(1, net.Server.FindPlayer("Ondra")!.Character.Traits.Generation);
    }

    /// <summary>Rytina Pradávného nese, že ji vyryl Pradávný. Po smrti už je hráč Nový.</summary>
    [Fact]
    public void SignKnowsAncientAuthor()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        var bits = SignBits.Empty();
        SignBits.Set(bits, 3, 3, true);
        var ancient = net.Server.Island.RuinSpots.Count;

        a.DrawSign((float)player.X, (float)player.Y, bits);

        Assert.True(net.Pump(() => a.Signs.Length == ancient + 1));
        Assert.True(a.Signs.Single(s => !s.Ancient).ByAncient);
    }

    private static void Kill(TestNetwork net, GameConnection connection, string nickname)
    {
        var player = net.Server.FindPlayer(nickname)!;
        player.Character.Hydration = 0;
        player.Character.Health = 0.0001;
        Assert.True(net.Pump(() => connection.Self!.Dead));
    }
}
