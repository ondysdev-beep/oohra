using Oohra.Core.Items;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Tělo mrtvého hráče a podpis přes síť.</summary>
public class CorpseServerTests
{
    /// <summary>Po smrti zůstanou věci u těla. Cizí si je prvních 24 hodin vzít nesmí, mrtvý po novém narození ano.</summary>
    [Fact]
    public void BodyKeepsItemsForOwner()
    {
        using var net = new TestNetwork(configure: c =>
        {
            c.StartSticks = 2;
            c.StartStones = 1;
        });
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var player = net.Server.FindPlayer("Ondra")!;
        Assert.True(net.Pump(() => a.Inventory.Stacks.Length == 2));

        player.Character.Hydration = 0;
        player.Character.Health = 0.0001;
        Assert.True(net.Pump(() => b.Corpses.Length == 1));
        var corpse = b.Corpses[0];
        Assert.Equal(a.PlayerId, corpse.OwnerId);
        Assert.Equal(3, corpse.Contents.Stacks.Sum(s => s.Count));
        Assert.True(corpse.TribeOnlyUntil > corpse.DiedAt);

        // Petr stojí u těla, ale není z kmene: nic si nevezme.
        var petr = net.Server.FindPlayer("Petr")!;
        petr.X = corpse.X;
        petr.Y = corpse.Y;
        b.TakeFromCorpse(corpse.Id, 0);
        Assert.True(net.Pump(() => b.TakeEvents().Any(e => e.Kind == EventKind.Corpse && e.Code == (byte)CorpseResult.TribeOnly)));

        // Ondra se narodí znovu, dojde k tělu a vezme si své věci.
        a.Reborn();
        Assert.True(net.Pump(() => a.Self is { Dead: false }));
        player.X = corpse.X;
        player.Y = corpse.Y;
        a.TakeFromCorpse(corpse.Id, 0);
        Assert.True(net.Pump(() => a.Inventory.Stacks.Any(s => s.Kind == ItemKind.Stick)));
        Assert.True(net.Pump(() => b.Corpses[0].Contents.Stacks.Length == 1));
    }

    [Fact]
    public void SignatureIsKeptAndSigned()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        var mark = SignBits.Empty();
        SignBits.Set(mark, 2, 2, true);

        a.SetSignature([3, 7], mark);
        Assert.True(net.Pump(() => a.SignatureName.Length == 2));
        Assert.True(SignBits.Get(a.SignatureMark, 2, 2));

        // Neplatný podpis (4 slabiky) se ignoruje.
        a.SetSignature([1, 2, 3, 4], mark);
        net.PumpFor(200);
        Assert.Equal(new byte[] { 3, 7 }, a.SignatureName);

        var bits = SignBits.Empty();
        SignBits.Set(bits, 5, 5, true);
        var ancient = net.Server.Island.RuinSpots.Count;
        a.DrawSign((float)player.X, (float)player.Y, bits);
        Assert.True(net.Pump(() => a.Signs.Length == ancient + 1));
        var sign = a.Signs.Single(s => !s.Ancient);
        Assert.Equal(new byte[] { 3, 7 }, sign.SignatureName);
        Assert.True(SignBits.Get(sign.SignatureMark!, 2, 2));
    }
}
