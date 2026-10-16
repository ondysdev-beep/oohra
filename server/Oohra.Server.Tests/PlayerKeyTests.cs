namespace Oohra.Server.Tests;

/// <summary>Tajný klíč hráče: za cizí postavu (stejná přezdívka, jiný klíč) hrát nejde.</summary>
public class PlayerKeyTests
{
    [Fact]
    public void OnlyOwnerKeyCanTakeCharacter()
    {
        using var net = new TestNetwork();
        var owner = net.Join("Ondra", playerKey: "muj-klic");
        Assert.True(owner.LoggedIn);
        owner.Dispose();
        Assert.True(net.Pump(() => net.Server.FindPlayer("Ondra") is { Online: false }));

        var thief = net.Join("ondra", playerKey: "cizi-klic");
        Assert.False(thief.LoggedIn);
        Assert.Contains("jiný hráč", thief.LoginError);

        var back = net.Join("Ondra", playerKey: "muj-klic");
        Assert.True(back.LoggedIn);
    }

    /// <summary>Server si pamatuje jen otisk klíče, ne klíč samotný.</summary>
    [Fact]
    public void StoresOnlyHash()
    {
        using var net = new TestNetwork();
        net.Join("Ondra", playerKey: "muj-klic");

        var stored = net.Server.FindPlayer("Ondra")!.KeyHash;
        Assert.NotNull(stored);
        Assert.DoesNotContain("muj-klic", stored);
        Assert.True(PlayerKeys.Matches(stored, "muj-klic"));
        Assert.False(PlayerKeys.Matches(stored, "jiny"));
        Assert.True(PlayerKeys.Matches(null, "kdokoli"));
    }
}
