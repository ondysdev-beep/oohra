namespace Oohra.Core.Tests;

public class GoldenDataTests
{
    // Ověří, že se golden data z prototypu kopírují k testům. Budou je potřebovat další testy.
    [Fact]
    public void GoldenWorldFileIsAvailable()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "golden", "golden_world.json");

        Assert.True(File.Exists(path), $"Chybí soubor {path}");
    }
}
