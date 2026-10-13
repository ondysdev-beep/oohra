namespace Oohra.Core.Tests;

public class GoldenDataTests
{
    // Ověří, že se golden data z prototypu kopírují k testům. Budou je potřebovat další testy.
    [Fact]
    public void GoldenWorldFileIsAvailable()
    {
        Assert.True(File.Exists(GoldenData.FilePath), $"Chybí soubor {GoldenData.FilePath}");
    }
}
