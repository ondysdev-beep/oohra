using Oohra.Core;
using Oohra.Core.Characters;
using Oohra.Core.Plants;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Server a klienti přes skutečnou síť (localhost). Brána Fáze C: dva klienti se vidí a server počítá jídlo a otravy.</summary>
public class ServerTests
{
    [Fact]
    public void LoginGivesWorldAndBody()
    {
        using var net = new TestNetwork();

        var client = net.Join("Ondra");

        Assert.True(client.LoggedIn);
        Assert.Equal(net.Server.Island.Plants.Count, client.World!.Plants.Length);
        Assert.Equal(net.Server.Island.Tiles, client.World.Tiles);
        Assert.Equal(70, client.Self!.Satiety, 1);
        Assert.Equal(0, client.Self.Generation);
    }

    [Fact]
    public void WrongInviteCodeIsRejected()
    {
        using var net = new TestNetwork();

        var client = net.Join("Ondra", "SPATNY-KOD");

        Assert.False(client.LoggedIn);
        Assert.Equal("Neplatný kód pozvánky.", client.LoginError);
        Assert.Null(client.World);
    }

    [Theory]
    [InlineData("x")]
    [InlineData("tohle-je-moc-dlouhe-jmeno")]
    [InlineData("<script>")]
    public void BadNicknameIsRejected(string nickname)
    {
        using var net = new TestNetwork();

        Assert.False(net.Join(nickname).LoggedIn);
    }

    [Fact]
    public void SameNicknameCannotPlayTwice()
    {
        using var net = new TestNetwork();
        net.Join("Ondra");

        var second = net.Join("ondra");

        Assert.False(second.LoggedIn);
    }

    // Brána Fáze C: dva klienti se vidí.
    [Fact]
    public void TwoPlayersSeeEachOther()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var b = net.Join("Petr");

        Assert.True(net.Pump(() => a.Snapshot?.Players.Length == 2 && b.Snapshot?.Players.Length == 2));
        Assert.Equal("Petr", a.Players[b.PlayerId].Nickname);
        Assert.Equal("Ondra", b.Players[a.PlayerId].Nickname);

        var bOnServer = net.Server.FindPlayer("Petr")!;
        Assert.True(net.Pump(() => a.Snapshot!.Players.Any(p => p.Id == b.PlayerId && Math.Abs(p.X - bOnServer.X) < 0.01)));
    }

    [Fact]
    public void LeavingPlayerDisappearsForOthers()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        net.Pump(() => a.Players.Count == 2);

        b.Dispose();

        Assert.True(net.Pump(() => a.Players.Count == 1 && a.Snapshot?.Players.Length == 1));
    }

    // Server hlídá pohyb: klient posílá jen směr, do moře se nedostane.
    [Fact]
    public void ServerMovesPlayerAndStopsAtObstacles()
    {
        using var net = new TestNetwork();
        var client = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        var startX = player.X;

        client.SendInput(1, 0);
        net.PumpFor(1500);

        Assert.True(player.X > startX);
        client.SendInput(1, 0.3f);
        net.PumpFor(3000);
        Assert.True(Movement.CanStand(net.Server.Island, player.X, player.Y, 0.28));
        Assert.NotEqual(TileType.Sea, net.Server.Island.TileAt((int)player.X, (int)player.Y));

        client.SendInput(0, 0);
        net.PumpFor(200);
        var stoppedAt = player.X;
        net.PumpFor(300);
        Assert.Equal(stoppedAt, player.X);
    }

    [Fact]
    public void HarvestAndEatAreDecidedByServer()
    {
        using var net = new TestNetwork();
        var client = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        var plant = net.Server.Island.Plants[0];

        client.Harvest(plant.Id, PlantPart.Leaf);
        Assert.True(net.Pump(() => client.TakeEvents().Any(e => e.Kind == EventKind.Harvest && e.Code == (byte)HarvestResult.TooFar)));

        player.X = plant.X;
        player.Y = plant.Y;
        client.Harvest(plant.Id, PlantPart.Leaf);
        Assert.True(net.Pump(() => client.Inventory.Stacks.Length == 1));
        Assert.Equal(plant.SpeciesId, client.Inventory.Stacks[0].SpeciesId);

        client.Eat(0);
        Assert.True(net.Pump(() => client.TakeEvents().Any(e => e.Kind == EventKind.Body && e.Code == (byte)BodyEvent.Ate)));
        Assert.True(net.Pump(() => client.Inventory.Stacks.Length == 0));
    }

    [Fact]
    public void DiggingRootHidesPlantForEveryone()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var player = net.Server.FindPlayer("Ondra")!;
        var plant = net.Server.Island.Plants[3];
        player.X = plant.X;
        player.Y = plant.Y;

        a.Harvest(plant.Id, PlantPart.Root);

        Assert.True(net.Pump(() => b.PlantVisuals[plant.Id] == PlantVisual.Gone));
    }

    // Brána Fáze C: jídlo a otravu počítá server, klient vidí jen příznaky.
    [Fact]
    public void PoisonSymptomsComeFromServer()
    {
        using var net = new TestNetwork(timeSpeed: 600);
        var client = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        // Nový (generace 1): bez železného žaludku Pradávných, jedna dávka toxinu přesáhne práh hlášky.
        player.Character = new Character(1, new Oohra.Core.Rng.Mulberry32(1));
        var poisonRoot = net.Server.Island.SpeciesSecrets.First(s => s.ArchetypeIndex == 1).Id;
        for (var i = 0; i < 3; i++)
        {
            player.Inventory.TryAdd(new PlantItem(poisonRoot, PlantPart.Root, Ripeness.Ripe, Season.Spring, TileType.Grass));
        }

        net.Pump(() => true);
        for (var i = 0; i < 3; i++)
        {
            client.Eat(0);
        }

        var events = new List<GameEvent>();
        var sawHeavyBody = net.Pump(() =>
        {
            player.Character.Satiety = player.Character.Hydration = 100;
            events.AddRange(client.TakeEvents());
            return client.Self!.Symptoms.Contains(Symptom.HeavyBody);
        }, 10000);

        Assert.True(sawHeavyBody);
        Assert.Contains(events, e => e.Kind == EventKind.Body && e.Code == (byte)BodyEvent.HeavyFeeling);
    }

    [Fact]
    public void DeathAndRebirthAsNextGeneration()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var player = net.Server.FindPlayer("Ondra")!;
        player.Character.Hydration = 0;
        player.Character.Health = 0.0001;

        Assert.True(net.Pump(() => a.Self!.Dead));
        Assert.Equal(DeathCause.Thirst, a.Self!.DeathCause);

        a.Reborn();
        Assert.True(net.Pump(() => a.Self is { Dead: false, Generation: 1 }));
        Assert.True(net.Pump(() => b.Players[a.PlayerId].Generation == 1));
    }

    [Fact]
    public void ReconnectKeepsCharacter()
    {
        using var net = new TestNetwork();
        var first = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        player.X = net.Server.Island.Plants[0].X;
        player.Y = net.Server.Island.Plants[0].Y;
        first.Harvest(0, PlantPart.Leaf);
        net.Pump(() => first.Inventory.Stacks.Length == 1);
        var id = first.PlayerId;

        first.Dispose();
        net.Pump(() => net.Server.OnlinePlayers == 0);
        var again = net.Join("Ondra");

        Assert.True(again.LoggedIn);
        Assert.Equal(id, again.PlayerId);
        Assert.True(net.Pump(() => again.Inventory.Stacks.Length == 1));
    }

    // Skrytá data nesmí jít na klienta: síťová knihovna (a tedy klient) Oohra.Core vůbec nezná.
    [Fact]
    public void ClientLibraryHasNoAccessToHiddenData()
    {
        var references = typeof(GameConnection).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        Assert.DoesNotContain("Oohra.Core", references);
        Assert.Contains("Oohra.Shared", references);
    }
}
