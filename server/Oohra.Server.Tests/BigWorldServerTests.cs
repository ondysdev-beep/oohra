using Oohra.Core.World;

namespace Oohra.Server.Tests;

/// <summary>Svět 384 × 384 (návrh 0.8) s podzemím pod ostrovem: klient dostane celou mapu a narodí se na souši.</summary>
public class BigWorldServerTests
{
    [Fact]
    public void ClientGetsWholeBigWorldAndIsBornOnLand()
    {
        using var net = new TestNetwork(configure: c => c.WorldSize = 384);
        var client = net.Join("Ondra");

        Assert.True(client.LoggedIn, client.LoginError);
        var world = client.World!;
        var island = net.Server.Island;
        Assert.Equal(384, island.SurfaceHeight);
        Assert.True(island.Height > 384, "Pod ostrovem je podzemí s jeskyněmi.");
        Assert.Equal(384 * island.Height, world.Tiles.Length);
        Assert.Contains(TileType.CaveFloor, world.Tiles);
        Assert.Equal(island.Tiles, world.Tiles);
        Assert.Equal(island.Shore, world.Shore);
        Assert.Contains(TileType.Ford, world.Tiles);
        Assert.Equal(island.Trees.Count, world.Trees.Length);

        var player = net.Server.FindPlayer("Ondra")!;
        Assert.False(Movement.IsWaterTile(island.TileAt((int)player.X, (int)player.Y)));
    }

    /// <summary>Když je kolem hráče hodně zvířat, dostane ta nejbližší, co se vejdou do paketu (server nespadne).</summary>
    [Fact]
    public void ManyAnimalsNearbyFitIntoOnePacket()
    {
        using var net = new TestNetwork(configure: c => c.Animals = true);
        var client = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        for (var i = 0; i < 150; i++)
        {
            net.Server.Animals.Place(AnimalKind.Rabbit, player.X + 1 + i % 15, player.Y + i / 15);
        }

        Assert.True(net.Pump(() => client.Animals.Length > 40));
        net.PumpFor(300);
        Assert.InRange(client.Animals.Length, 40, 60);
    }
}
