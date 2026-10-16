namespace Oohra.Server.Tests;

/// <summary>Přezdívky můžou být jakékoli, jen ne sprosté (seznam blocked_words.json).</summary>
public class NicknameFilterTests
{
    private static readonly NicknameFilter Filter = NicknameFilter.Load(Path.Combine(AppContext.BaseDirectory, "blocked_words.json"));

    [Theory]
    [InlineData("Kurva")]
    [InlineData("kuuurva123")]
    [InlineData("K0K0T")]
    [InlineData("Píčus")]
    [InlineData("zmrd_jeden")]
    [InlineData("FuckYou")]
    [InlineData("sh1t")]
    [InlineData("H i t l e r")]
    public void BlocksVulgarNames(string nickname)
    {
        Assert.True(Filter.IsBlocked(nickname), nickname);
    }

    [Theory]
    [InlineData("Ondra")]
    [InlineData("Petr Novák")]
    [InlineData("Picasso")]
    [InlineData("Lovec_12")]
    [InlineData("Šárka")]
    [InlineData("Kuba")]
    public void AllowsNormalNames(string nickname)
    {
        Assert.False(Filter.IsBlocked(nickname), nickname);
    }

    [Fact]
    public void ServerRefusesVulgarNickname()
    {
        using var net = new TestNetwork();

        var client = net.Join("Kokot");

        Assert.False(client.LoggedIn);
        Assert.Contains("sprosté", client.LoginError);
    }
}
