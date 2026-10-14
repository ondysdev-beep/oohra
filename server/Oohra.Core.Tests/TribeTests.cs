using Oohra.Core.Items;
using Oohra.Core.Social;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Kmeny pozvánkou, zásoby kmene a kdo smí sahat na cizí stavby (návrh 14.1 a 14.2).</summary>
public class TribeTests
{
    private const int Ondra = 1;
    private const int Petr = 2;
    private const int Jana = 3;
    private const int Eva = 4;

    [Fact]
    public void InvitationMakesTribe()
    {
        var tribes = new Tribes();
        Assert.False(tribes.SameTribe(Ondra, Petr));

        Assert.Equal(TribeResult.Invited, tribes.Invite(Ondra, 0, 0, Petr, 2, 0, now: 0));
        Assert.Equal(TribeResult.Joined, tribes.Answer(Petr, Ondra, accept: true, now: 1));

        Assert.True(tribes.SameTribe(Ondra, Petr));
        Assert.Equal([Ondra, Petr], tribes.MembersWith(Petr));
        Assert.Equal(TribeResult.AlreadyTogether, tribes.Invite(Ondra, 0, 0, Petr, 1, 0, now: 2));
    }

    [Fact]
    public void InvitationNeedsToBeNearAndFresh()
    {
        var tribes = new Tribes();

        Assert.Equal(TribeResult.TooFar, tribes.Invite(Ondra, 0, 0, Petr, 5, 0, now: 0));
        Assert.Equal(TribeResult.NoInvite, tribes.Answer(Petr, Ondra, true, now: 0));

        tribes.Invite(Ondra, 0, 0, Petr, 1, 0, now: 0);
        Assert.Equal(TribeResult.NoInvite, tribes.Answer(Petr, Jana, true, now: 1));
        Assert.Equal(TribeResult.NoInvite, tribes.Answer(Petr, Ondra, true, now: 11));

        tribes.Invite(Ondra, 0, 0, Petr, 1, 0, now: 20);
        Assert.Equal(TribeResult.Declined, tribes.Answer(Petr, Ondra, false, now: 21));
        Assert.False(tribes.SameTribe(Ondra, Petr));
        Assert.Equal(0, tribes.Version);
    }

    /// <summary>Kdo přijme pozvánku jiného kmene, přejde. Kmen, kde zůstane jeden člověk, zanikne.</summary>
    [Fact]
    public void JoiningAnotherTribeLeavesTheOld()
    {
        var tribes = new Tribes();
        Join(tribes, Ondra, Petr);
        Join(tribes, Jana, Eva);

        Join(tribes, Jana, Petr);

        Assert.True(tribes.SameTribe(Petr, Jana));
        Assert.True(tribes.SameTribe(Petr, Eva));
        Assert.Null(tribes.TribeOf(Ondra));
        Assert.Equal([Ondra], tribes.MembersWith(Ondra));
    }

    [Fact]
    public void LeavingTribe()
    {
        var tribes = new Tribes();
        Join(tribes, Ondra, Petr);
        Join(tribes, Ondra, Jana);

        Assert.Equal(TribeResult.Left, tribes.Leave(Jana));
        Assert.True(tribes.SameTribe(Ondra, Petr));
        Assert.Equal(TribeResult.Left, tribes.Leave(Petr));
        Assert.Null(tribes.TribeOf(Ondra));
        Assert.Equal(TribeResult.NotInTribe, tribes.Leave(Petr));
    }

    /// <summary>Cizí zeď rozebere jen majitel a jeho kmen.</summary>
    [Fact]
    public void OnlyOwnerAndTribeRemoveStructure()
    {
        var tribes = new Tribes();
        var structures = new Structures();
        var wall = structures.Place(BuildKind.StickWall, 10, 10, 0, ownerId: Ondra);
        var bag = new Inventory(10);

        Assert.Equal(BuildResult.NotYours, structures.Remove(wall.Id, bag, 10, 10.5, Petr, tribes));
        Join(tribes, Ondra, Petr);
        Assert.Equal(BuildResult.Removed, structures.Remove(wall.Id, bag, 10, 10.5, Petr, tribes));
    }

    [Fact]
    public void TrapCatchOnlyForOwnerAndTribe()
    {
        var tribes = new Tribes();
        var structures = new Structures();
        var trap = structures.Place(BuildKind.Trap, 10, 10, 0, length: 0.01, ownerId: Ondra);
        structures.Catch(trap, AnimalKind.Rabbit);

        Assert.Equal(HuntResult.NotYours, structures.TakeCatch(trap.Id, new Inventory(10), 10, 10.5, 0, Petr, tribes));
        Assert.Equal(HuntResult.Butchered, structures.TakeCatch(trap.Id, new Inventory(10), 10, 10.5, 0, Ondra, tribes));
    }

    /// <summary>Zásoby kmene: člen ukládá i bere, cizí nesmí, plné zásoby nejdou rozebrat.</summary>
    [Fact]
    public void StockpileIsShared()
    {
        var island = IslandGenerator.Build(1, 128, 128);
        var (x, y) = OpenSpot(island);
        var tribes = new Tribes();
        var ondrasBag = Sticks(5);
        ondrasBag.TryAdd(Item.Stone);

        Assert.Equal(BuildResult.Built, island.Structures.Build(island, new Fires(), ondrasBag, x, y, BuildKind.Stockpile, x + 1, y, 0, 0, [], ownerId: Ondra));
        var stockpile = island.Structures.All.Single();
        Assert.Equal(Ondra, stockpile.OwnerId);
        Assert.Equal(1, ondrasBag.Count(ItemKind.Stick));

        Assert.Equal(TribeResult.Stored, island.Structures.StoreItem(stockpile.Id, ondrasBag, 1, x, y, Ondra, tribes));
        Assert.Equal(1, stockpile.Store!.Count(ItemKind.Stone));
        Assert.Equal(1, island.Structures.StockVersion);

        var petrsBag = new Inventory(10);
        Assert.Equal(TribeResult.NotYours, island.Structures.TakeItem(stockpile.Id, petrsBag, 0, x, y, Petr, tribes));
        Join(tribes, Ondra, Petr);
        Assert.Equal(TribeResult.TooFar, island.Structures.TakeItem(stockpile.Id, petrsBag, 0, x - 3, y, Petr, tribes));
        Assert.Equal(BuildResult.NotEmpty, island.Structures.Remove(stockpile.Id, petrsBag, x, y, Petr, tribes));
        Assert.Equal(TribeResult.Taken, island.Structures.TakeItem(stockpile.Id, petrsBag, 0, x, y, Petr, tribes));
        Assert.Equal(1, petrsBag.Count(ItemKind.Stone));
        Assert.Equal(TribeResult.NotFound, island.Structures.TakeItem(stockpile.Id, petrsBag, 0, x, y, Petr, tribes));

        Assert.Equal(BuildResult.Removed, island.Structures.Remove(stockpile.Id, petrsBag, x, y, Petr, tribes));
        Assert.Equal(2, petrsBag.Count(ItemKind.Stick));
    }

    [Fact]
    public void FullStockpileRefuses()
    {
        var structures = new Structures();
        var stockpile = structures.Place(BuildKind.Stockpile, 10, 10, 0, length: 0.01, ownerId: Ondra);
        for (var day = 0; day < stockpile.Store!.Capacity; day++)
        {
            stockpile.Store.TryAdd(Item.RawMeat(day));
        }

        var bag = new Inventory(10);
        bag.TryAdd(Item.Stone);

        Assert.Equal(TribeResult.StockFull, structures.StoreItem(stockpile.Id, bag, 0, 10, 10.5, Ondra, new Tribes()));
        Assert.Single(bag.Stacks);
    }

    [Fact]
    public void MoveStackMovesWhatFits()
    {
        var from = new Inventory(5);
        for (var i = 0; i < 4; i++)
        {
            from.TryAdd(Item.Stick);
        }

        var to = new Inventory(5);
        Assert.Equal(4, from.MoveStack(0, to));
        Assert.Empty(from.Stacks);
        Assert.Equal(4, to.Count(ItemKind.Stick));
        Assert.Equal(0, from.MoveStack(0, to));
    }

    [Fact]
    public void TribeMemberErasesSign()
    {
        var island = IslandGenerator.Build(1, 128, 128);
        var (x, y) = OpenSpot(island);
        var tribes = new Tribes();
        var bits = SignBits.Empty();
        SignBits.Set(bits, 1, 1, true);
        island.Signs.Draw(island, Ondra, x, y, x, y, bits, 0);
        var sign = island.Signs.All.Single();

        Assert.Equal(SignResult.NotYours, island.Signs.Erase(sign.Id, Petr, x, y, tribes));
        Join(tribes, Ondra, Petr);
        Assert.Equal(SignResult.Erased, island.Signs.Erase(sign.Id, Petr, x, y, tribes));
    }

    private static void Join(Tribes tribes, int inviter, int invitee)
    {
        Assert.Equal(TribeResult.Invited, tribes.Invite(inviter, 0, 0, invitee, 1, 0, 100));
        Assert.Equal(TribeResult.Joined, tribes.Answer(invitee, inviter, true, 101));
    }

    private static Inventory Sticks(int count)
    {
        var inventory = new Inventory(10);
        for (var i = 0; i < count; i++)
        {
            inventory.TryAdd(Item.Stick);
        }

        return inventory;
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
