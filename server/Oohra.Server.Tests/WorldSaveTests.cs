using Newtonsoft.Json;
using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.Social;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Ukládání světa: po restartu serveru je všechno, jak bylo (hráči, postavy, stavby, značky, těla, kmeny, čas).</summary>
public sealed class WorldSaveTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"oohra-svet-{Guid.NewGuid():N}");

    private string WorldFile => Path.Combine(_directory, "world.json");

    [Fact]
    public void WorldSurvivesRestart()
    {
        ServerSave before;
        int ondraId;
        using (var net = new TestNetwork(configure: Saving(c =>
        {
            c.StartSticks = 4;
            c.StartStones = 2;
        })))
        {
            var a = net.Join("Ondra", playerKey: "klic-ondry");
            var b = net.Join("Petr", playerKey: "klic-petra");
            ondraId = a.PlayerId;
            var ondra = net.Server.FindPlayer("Ondra")!;
            var petr = net.Server.FindPlayer("Petr")!;
            petr.X = ondra.X;
            petr.Y = ondra.Y;

            // Kmen, koš se zásobami, značka a tělo.
            a.InviteToTribe(b.PlayerId);
            Assert.True(net.Pump(() => b.TakeInvites().Contains(a.PlayerId)));
            b.AnswerInvite(a.PlayerId, true);
            Assert.True(net.Pump(() => a.TribeMembers.Length == 2));
            a.Place(BuildKind.Stockpile, (float)ondra.X + 1, (float)ondra.Y, 0);
            Assert.True(net.Pump(() => a.Stockpiles.Length == 1));
            a.StoreItem(a.Stockpiles[0].StructureId, Array.FindIndex(a.Inventory.Stacks, s => s.Kind == ItemKind.Stone));
            Assert.True(net.Pump(() => a.Stockpiles[0].Contents.Stacks.Length == 1));
            var bits = SignBits.Empty();
            SignBits.Set(bits, 1, 1, true);
            a.DrawSign((float)ondra.X, (float)ondra.Y - 1, bits);
            Assert.True(net.Pump(() => a.Signs.Any(s => !s.Ancient)));

            // Věc na zemi (klacky se spotřebovaly na koš).
            ondra.Inventory.TryAdd(Item.Stick);
            Assert.True(net.Pump(() => a.Inventory.Stacks.Any(s => s.Kind == ItemKind.Stick)));
            a.Drop(Array.FindIndex(a.Inventory.Stacks, s => s.Kind == ItemKind.Stick));
            Assert.True(net.Pump(() => a.GroundItems.Length == 1));

            petr.Character.Hydration = 0;
            petr.Character.Health = 0.0001;
            Assert.True(net.Pump(() => b.Self!.Dead && net.Server.Island.Corpses.All.Count == 1));

            // Skrytý stav postavy: jed v těle a otrava, která se teprve projeví.
            ondra.Character.Toxin = 12.5;
            ondra.Character.TorchMinutes = 9999;
            ondra.Character.Learn("smallBag");
            Diseases.Start(ondra.Character, DiseaseKind.MarshFever, 60, net.Server.Clock.TotalMinutes);
            ondra.Character.Leeches = 2;
            ondra.Character.AddTick(net.Server.Clock.TotalMinutes - 5);
            ondra.Character.LearnCave(3);
            var meat = new Oohra.Core.Items.Inventory(2);
            meat.TryAdd(Oohra.Core.Items.Item.RawMeat(net.Server.Clock.Day));
            Assert.Equal(PreserveResult.Hung, net.Server.Racks.Hang(RackHolder.Structure, net.Server.Island.Structures.All.First().Id, meat, 0, net.Server.Clock.TotalMinutes));
            var nest = net.Server.Island.Wasps.Active(0).First();
            ondra.Character.TorchMinutes = 9999;
            Assert.Equal(WaspResult.SmokedOut, net.Server.Island.Wasps.SmokeOut(nest.TileIndex, ondra.Character, nest.X, nest.Y, net.Server.Clock.TotalMinutes));
            var journal = net.Server.Journals.Create();
            net.Server.Journals.Draw(journal.Ref, 0, Enumerable.Repeat((byte)255, SignBits.ByteCount).ToArray());
            ondra.Inventory.TryAdd(journal);
            ondra.Inventory.TryAdd(new Oohra.Core.Items.Item(Oohra.Core.Items.ItemKind.LargeBag));

            // Kácení: rozsekaný strom (jedno seknutí) se uloží.
            var island = net.Server.Island;
            var tree = island.Trees.First(t => island.HasTree(t.TileIndex));
            var axe = new Oohra.Core.Items.Inventory(5);
            axe.TryAdd(new Oohra.Core.Items.Item(Oohra.Core.Items.ItemKind.Axe, Uses: 10));
            Assert.Equal(WoodResult.Chopped, island.Forestry.Chop(island, (int)tree.X, (int)tree.Y, 99, tree.X - 1, tree.Y, axe));
            ondra.Character.Schedule(net.Server.Clock.TotalMinutes + 30, EffectType.Vomit, 0.7);
            ondra.Character.Vocabulary.Hear([1, 2], 1, seesSpeaker: true);
            before = net.Server.CaptureWorld();
        }

        Assert.True(File.Exists(WorldFile));
        using var restarted = new TestNetwork(configure: Saving());
        var after = restarted.Server.CaptureWorld();

        // Stejný stav (kromě času uložení).
        Assert.Equal(Json(before, saved: true), Json(after, saved: true));
        Assert.Equal(before.CreatedUtc, restarted.Server.WorldCreatedUtc);
        Assert.Equal(12.5, restarted.Server.FindPlayer("Ondra")!.Character.Toxin);
        Assert.True(restarted.Server.FindPlayer("Ondra")!.Character.TorchLit);
        Assert.True(restarted.Server.FindPlayer("Ondra")!.Character.Knows("smallBag"));
        Assert.True(restarted.Server.FindPlayer("Ondra")!.Character.HasDisease(DiseaseKind.MarshFever));
        Assert.Equal(2, restarted.Server.FindPlayer("Ondra")!.Character.Leeches);
        Assert.Single(restarted.Server.FindPlayer("Ondra")!.Character.Ticks);
        Assert.True(restarted.Server.FindPlayer("Ondra")!.Character.KnowsCave(3));
        Assert.Single(restarted.Server.Island.Wasps.Export());
        Assert.Single(restarted.Server.Racks.Export());
        var savedJournal = restarted.Server.FindPlayer("Ondra")!.Inventory.Stacks.Single(s => s.Item.Kind == Oohra.Core.Items.ItemKind.Journal).Item;
        Assert.True(SignBits.Get(restarted.Server.Journals.Pages(savedJournal.Ref)![0], 0, 0));
        Assert.Equal(Oohra.Core.Items.ItemKind.LargeBag, restarted.Server.FindPlayer("Ondra")!.Inventory.Bag!.Kind);
        Assert.Contains(restarted.Server.Island.Trees, t => restarted.Server.Island.Forestry.HitsOf(t.TileIndex) == 1);
        Assert.Single(restarted.Server.FindPlayer("Ondra")!.Character.Scheduled);
        Assert.True(restarted.Server.FindPlayer("Petr")!.Character.IsDead);

        // Cizí klíč se ke staré postavě nedostane, vlastní ano (se stejným číslem hráče).
        var thief = restarted.Join("Ondra", playerKey: "jiny");
        Assert.False(thief.LoggedIn);
        var back = restarted.Join("Ondra", playerKey: "klic-ondry");
        Assert.True(back.LoggedIn);
        Assert.Equal(ondraId, back.PlayerId);
        Assert.True(restarted.Pump(() => back.Stockpiles.Length == 1 && back.Signs.Any(s => !s.Ancient) && back.Corpses.Length == 1
            && back.GroundItems.Any(g => g.Stack.Kind == ItemKind.Stick)));
        Assert.Equal(2, back.TribeMembers.Length);

        // Nový hráč dostane nové číslo (čísla se neopakují).
        var newcomer = restarted.Join("Jana");
        Assert.True(newcomer.PlayerId > ondraId);
    }

    /// <summary>Uložený svět z jiného seedu se nenačte (změny by se přenesly na jiný ostrov) a server řekne proč.</summary>
    [Fact]
    public void RefusesWorldFromOtherSeed()
    {
        using (new TestNetwork(configure: Saving()))
        {
        }

        var error = Assert.Throws<InvalidOperationException>(() => new TestNetwork(configure: Saving(c => c.Seed = 777)));
        Assert.Contains("seed", error.Message);
    }

    /// <summary>Záloha vznikne hned při prvním uložení, pak jednou za backupEveryMinutes.</summary>
    [Fact]
    public void MakesBackup()
    {
        using (var net = new TestNetwork(configure: Saving()))
        {
            net.Join("Ondra");
        }

        Assert.Single(Directory.GetFiles(Path.Combine(_directory, "zalohy"), "svet-*.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private Action<ServerConfig> Saving(Action<ServerConfig>? more = null)
    {
        return c =>
        {
            c.WorldFile = WorldFile;
            more?.Invoke(c);
        };
    }

    private static string Json(ServerSave save, bool saved)
    {
        var json = JsonConvert.SerializeObject(save, new JsonSerializerSettings { FloatFormatHandling = FloatFormatHandling.String });
        return saved ? json.Replace(JsonConvert.SerializeObject(save.SavedUtc), string.Empty) : json;
    }
}
