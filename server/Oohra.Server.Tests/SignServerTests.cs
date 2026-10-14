using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Značky přes síť: staré rytiny, kreslení, mazání.</summary>
public class SignServerTests
{
    [Fact]
    public void AncientCarvingsAreSentOnLogin()
    {
        using var net = new TestNetwork();
        var client = net.Join("Ondra");

        Assert.True(net.Pump(() => client.Signs.Length == net.Server.Island.RuinSpots.Count));
        Assert.All(client.Signs, s => Assert.True(s.Ancient && s.Carved && s.RecordSpeciesId >= 0 && s.RecordName.Length == 2));
        Assert.All(client.Signs, s => Assert.True(SignBits.Count(s.Bits) > 0));
    }

    [Fact]
    public void DrawSeenByOthersAndErasedOnlyByAuthor()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var ancient = net.Server.Island.RuinSpots.Count;
        var player = net.Server.FindPlayer("Ondra")!;
        var bits = SignBits.Empty();
        SignBits.Set(bits, 3, 4, true);

        a.DrawSign((float)player.X, (float)player.Y, bits);

        Assert.True(net.Pump(() => a.TakeEvents().Any(e => e.Kind == EventKind.Sign && e.Code == (byte)SignResult.Drawn)));
        Assert.True(net.Pump(() => b.Signs.Length == ancient + 1));
        var sign = b.Signs.Single(s => !s.Ancient);
        Assert.Equal(a.PlayerId, sign.AuthorId);
        Assert.True(SignBits.Get(sign.Bits, 3, 4));
        Assert.True(sign.WeathersAt > sign.CreatedAt || sign.Carved);

        var petr = net.Server.FindPlayer("Petr")!;
        petr.X = player.X;
        petr.Y = player.Y;
        b.EraseSign(sign.Id);
        Assert.True(net.Pump(() => b.TakeEvents().Any(e => e.Kind == EventKind.Sign && e.Code == (byte)SignResult.NotYours)));

        a.EraseSign(sign.Id);
        Assert.True(net.Pump(() => b.Signs.Length == ancient));
    }
}
