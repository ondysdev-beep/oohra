using Oohra.Core.Plants;

namespace Oohra.Core.Tests;

public class FruitCalendarTests
{
    private static readonly PlantSpecies _species = new()
    {
        Id = 0,
        Hue = 100,
        Saturation = 40,
        Lightness = 40,
        FruitHue = 0,
        Shape = 0,
        StartMonth = 3,
    };

    // Plody: 2 měsíce nezralé, 2 zralé, 2 přezrálé, pak do dalšího roku nic.
    [Theory]
    [InlineData(3.5, Ripeness.Unripe)]
    [InlineData(5.5, Ripeness.Ripe)]
    [InlineData(7.5, Ripeness.Overripe)]
    [InlineData(15.5, Ripeness.Unripe)]
    public void StageFollowsCalendar(double month, Ripeness expected)
    {
        Assert.Equal(expected, FruitCalendar.Stage(_species, 0, month, 12));
    }

    [Theory]
    [InlineData(2.0)]
    [InlineData(9.0)]
    [InlineData(11.9)]
    public void NoFruitOutsideSeason(double month)
    {
        Assert.Null(FruitCalendar.Stage(_species, 0, month, 12));
    }

    [Fact]
    public void OffsetShiftsRipening()
    {
        Assert.Equal(Ripeness.Ripe, FruitCalendar.Stage(_species, 1, 4.5, 12));
        Assert.Equal(Ripeness.Unripe, FruitCalendar.Stage(_species, 0, 4.5, 12));
    }
}
