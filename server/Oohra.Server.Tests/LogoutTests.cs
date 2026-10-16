namespace Oohra.Server.Tests;

/// <summary>Po odpojení zůstane postava chvíli ve světě, aby se odpojením nedalo utéct před vlkem.</summary>
public class LogoutTests
{
    [Fact]
    public void CharacterStaysAfterDisconnect()
    {
        using var net = new TestNetwork(configure: c => c.LogoutLingerSeconds = 1.5);
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        Assert.True(net.Pump(() => b.Players.ContainsKey(a.PlayerId)));

        a.Dispose();

        // Hned po odpojení ho Petr pořád vidí ve světě (a zvířata na něj můžou útočit).
        Assert.True(net.Pump(() => net.Server.FindPlayer("Ondra") is { Online: false }));
        net.PumpFor(300);
        Assert.True(b.Players.ContainsKey(a.PlayerId));
        Assert.Contains(b.Snapshot!.Players, p => p.Id == a.PlayerId);

        // Za chvíli postava zmizí.
        Assert.True(net.Pump(() => !b.Players.ContainsKey(a.PlayerId), 5000));
    }

    /// <summary>Kdo se vrátí dřív, než postava zmizí, pokračuje, jako by neodešel.</summary>
    [Fact]
    public void ReturningPlayerKeepsCharacter()
    {
        using var net = new TestNetwork(configure: c => c.LogoutLingerSeconds = 60);
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var id = a.PlayerId;
        a.Dispose();
        Assert.True(net.Pump(() => net.Server.FindPlayer("Ondra") is { Online: false }));

        var again = net.Join("Ondra");

        Assert.True(again.LoggedIn);
        Assert.Equal(id, again.PlayerId);
        net.PumpFor(300);
        Assert.True(b.Players.ContainsKey(id));
    }
}
