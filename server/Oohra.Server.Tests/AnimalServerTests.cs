using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Zvířata a lov přes síť.</summary>
public class AnimalServerTests
{
    [Fact]
    public void PlayerGetsAnimalsNearby()
    {
        using var net = new TestNetwork(configure: c => c.Animals = true);
        var client = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        net.Server.Animals.Place(AnimalKind.Deer, player.X + 5, player.Y);

        Assert.True(net.Pump(() => client.Animals.Length > 0));
        Assert.All(client.Animals, a => Assert.True(Math.Abs(a.X - player.X) < 41 && Math.Abs(a.Y - player.Y) < 41));
    }

    [Fact]
    public void CatchRabbitButcherAndEat()
    {
        using var net = new TestNetwork(configure: c => c.Animals = true);
        var client = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        var rabbit = net.Server.Animals.Place(AnimalKind.Rabbit, player.X + 0.5, player.Y);

        client.Attack(rabbit.Id);
        Assert.True(net.Pump(() => client.TakeEvents().Any(e =>
            e.Kind == EventKind.Hunt && e.Code == (byte)HuntResult.CaughtSmall && e.Subject == (byte)AnimalKind.Rabbit)));

        client.Butcher(rabbit.Id);
        Assert.True(net.Pump(() => client.Inventory.Stacks.Any(s => s.Kind == ItemKind.Meat)));

        var satiety = client.Self!.Satiety;
        client.Eat(0);
        Assert.True(net.Pump(() => client.Self!.Satiety > satiety + 10));
    }

    [Fact]
    public void SpearFromBackpack()
    {
        using var net = new TestNetwork(configure: c =>
        {
            c.StartSticks = 1;
            c.StartStones = 1;
        });
        var client = net.Join("Ondra");

        client.MakeSpear();

        Assert.True(net.Pump(() => client.Inventory.Stacks.Length == 1 && client.Inventory.Stacks[0].Kind == ItemKind.Spear));
    }

    /// <summary>Divočák hráče zraní: hráč krvácí, kulhá a dostane hlášku.</summary>
    [Fact]
    public void BoarWoundsPlayer()
    {
        using var net = new TestNetwork(configure: c => c.Animals = true);
        var client = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        var (x, y) = OpenSpot(net.Server.Island);
        player.X = x;
        player.Y = y;
        net.Server.Animals.Place(AnimalKind.Boar, x + 2.5, y);

        Assert.True(net.Pump(() => client.Self!.Symptoms.Contains(Symptom.Bleeding), timeoutMs: 8000));
        Assert.Contains(client.TakeEvents(), e => e.Kind == EventKind.Body && e.Code == (byte)BodyEvent.WoundedByBoar);
    }

    private static (double X, double Y) OpenSpot(Island island)
    {
        for (var i = 0; i < island.Tiles.Length; i++)
        {
            var (x, y) = (i % island.Width, i / island.Width);
            if (island.Tiles[i] == TileType.Grass
                && Enumerable.Range(-3, 7).All(dy => Enumerable.Range(-3, 7).All(dx => !Movement.IsBlocked(island, x + dx, y + dy))))
            {
                return (x + 0.5, y + 0.5);
            }
        }

        throw new InvalidOperationException("Na ostrově není volné místo.");
    }
}
