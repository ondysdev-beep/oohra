using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Značky a nákresy: kreslení, zvětrávání, mazání a staré rytiny (prototyp signs, bitsForShape a makeRuins).</summary>
public class SignTests
{
    private const double Day = 1440;
    private static readonly Island _island = WorldGenerator.Build(1, 128, 128);

    /// <summary>Kresby tvarů rostlin stejné jako bitsForShape v prototypu.</summary>
    [Theory]
    [InlineData(0, "000000000000000011110000000001100000000001100000010000000010011101101110011101101110010000000010000001100000000001100000000011110000000000000000")]
    [InlineData(1, "000001000000000001000000011001001100001001001000001101011000000101010000000111110000000111110000000011100000000011100000000001000000000001000000")]
    [InlineData(2, "000000000000000000000000001110011100011111111110011111111110011111111110001111111100000111111000000011110000000011110000000001100000000000000000")]
    [InlineData(3, "000000000000000000000000000001100000000001100000000011110000000011110000000111111000000111111000001111111100001111111100001111111100000000000000")]
    public void ShapeBitsMatchPrototype(int shape, string expected)
    {
        Assert.Equal(expected, SignBits.ToText(RuinCarvings.ShapeBits(shape)));
    }

    /// <summary>
    /// Staré rytiny jako makeRuins v prototypu: rostlina, jméno a poznámky závisí jen na seedu a počtu zřícenin (6),
    /// takže sedí s prototypem i v novém světě. Polohy jsou zříceniny nového světa.
    /// </summary>
    [Theory]
    [InlineData(1, "0:7-6:Vomit|0:6-5:Filling|0:7-5:Faint|0:4-3:Vomit|4:0-7:Bitter+Fever|3:7-6:Filling")]
    [InlineData(12345, "2:4-3:Filling+Bitter+Fever|0:2-6:Filling|3:3-6:Filling|0:6-3:|6:9-5:Fever+Tremor+Faint|6:1-9:Tremor")]
    public void RuinCarvingsMatchPrototype(int seed, string expected)
    {
        var island = WorldGenerator.Build(seed);
        var carvings = RuinCarvings.For(island);
        var text = string.Join("|", carvings.Select(c =>
            FormattableString.Invariant($"{c.Record.SpeciesId}:{c.Record.Name[0]}-{c.Record.Name[1]}:{string.Join("+", c.Record.Tags)}")));

        Assert.Equal(expected, text);
        Assert.Equal(island.RuinSpots, carvings.Select(c => new Position(c.X, c.Y)));
    }

    [Fact]
    public void DrawOnGroundAndOnRock()
    {
        var signs = new Signs();
        var (gx, gy) = Tile(TileType.Grass);
        var (rx, ry) = Tile(TileType.Rock);

        Assert.Equal(SignResult.Empty, signs.Draw(_island, 1, gx, gy, gx, gy, SignBits.Empty(), 0));
        Assert.Equal(SignResult.TooFar, signs.Draw(_island, 1, gx - 3, gy, gx, gy, Dot(), 0));
        Assert.Equal(SignResult.Drawn, signs.Draw(_island, 1, gx, gy, gx, gy, Dot(), 0));
        Assert.Equal(SignResult.CannotDrawHere, signs.Draw(_island, 1, gx, gy, gx + 0.3, gy, Dot(), 0));
        Assert.Equal(SignResult.Drawn, signs.Draw(_island, 1, rx, ry, rx, ry, Dot(), 0));

        Assert.False(signs.All[0].Carved);
        Assert.True(signs.All[1].Carved);
        Assert.Equal(2, signs.Version);
    }

    /// <summary>Značka si pamatuje, že ji nakreslil Pradávný (návrh 8.6, rytiny pro věky).</summary>
    [Fact]
    public void RemembersAncientAuthor()
    {
        var signs = new Signs();
        var (rx, ry) = Tile(TileType.Rock);

        Assert.Equal(SignResult.Drawn, signs.Draw(_island, 1, rx, ry, rx, ry, Dot(), 0, byAncient: true));

        Assert.True(signs.All[0].ByAncient);
        Assert.False(signs.All[0].Ancient);
    }

    [Fact]
    public void CannotDrawIntoWater()
    {
        var (wx, wy) = Tile(TileType.Pond);

        Assert.Equal(SignResult.CannotDrawHere, new Signs().Draw(_island, 1, wx, wy, wx, wy, Dot(), 0));
    }

    /// <summary>Kresba v hlíně za 5 dní zvětrá, rytina na skále a stará rytina zůstanou.</summary>
    [Fact]
    public void GroundDrawingsWeather()
    {
        var signs = new Signs();
        signs.AddAncient(_island);
        var ancient = signs.All.Count;
        var (gx, gy) = Tile(TileType.Grass);
        var (rx, ry) = Tile(TileType.Rock);
        signs.Draw(_island, 1, gx, gy, gx, gy, Dot(), Day);
        signs.Draw(_island, 1, rx, ry, rx, ry, Dot(), Day);

        signs.Weather(5.9 * Day);
        Assert.Equal(ancient + 2, signs.All.Count);
        signs.Weather(6 * Day);
        Assert.Equal(ancient + 1, signs.All.Count);
        Assert.True(signs.All.Last().Carved);
    }

    [Fact]
    public void OnlyAuthorErases()
    {
        var signs = new Signs();
        signs.AddAncient(_island);
        var ruin = signs.All[0];
        var (gx, gy) = Tile(TileType.Grass);
        signs.Draw(_island, 1, gx, gy, gx, gy, Dot(), 0);
        var mine = signs.All.Last();

        Assert.Equal(SignResult.NotYours, signs.Erase(mine.Id, 2, gx, gy));
        Assert.Equal(SignResult.NotYours, signs.Erase(ruin.Id, 1, ruin.X, ruin.Y));
        Assert.Equal(SignResult.TooFar, signs.Erase(mine.Id, 1, gx + 5, gy));
        Assert.Equal(SignResult.Erased, signs.Erase(mine.Id, 1, gx, gy));
        Assert.Equal(SignResult.NotFound, signs.Erase(mine.Id, 1, gx, gy));
    }

    [Fact]
    public void LimitPerAuthor()
    {
        var signs = new Signs();
        var (gx, gy) = Tile(TileType.Grass);
        var drawn = 0;
        for (var i = 0; i < 400 && drawn < 30; i++)
        {
            var (x, y) = (gx + (i % 20) * 0.7 - 7, gy + (i / 20) * 0.7 - 7);
            if (signs.Draw(_island, 1, x, y, x, y, Dot(), 0) == SignResult.Drawn)
            {
                drawn++;
            }
        }

        Assert.Equal(30, drawn);
        var free = FreeSpot(signs);
        Assert.Equal(SignResult.TooMany, signs.Draw(_island, 1, free.X, free.Y, free.X, free.Y, Dot(), 0));
        Assert.Equal(SignResult.Drawn, signs.Draw(_island, 2, free.X, free.Y, free.X, free.Y, Dot(), 0));
    }

    [Fact]
    public void BitsRoundTrip()
    {
        var bits = SignBits.Empty();
        SignBits.Set(bits, 11, 11, true);
        SignBits.Set(bits, 0, 0, true);
        SignBits.Set(bits, 0, 0, false);

        Assert.Equal(1, SignBits.Count(bits));
        Assert.True(SignBits.Get(bits, 11, 11));
        Assert.Equal(bits, SignBits.FromText(SignBits.ToText(bits)));
    }

    private static byte[] Dot()
    {
        var bits = SignBits.Empty();
        SignBits.Set(bits, 5, 5, true);
        return bits;
    }

    /// <summary>Střed dlaždice daného druhu, kolem které je celé okolí 3 × 3 stejné.</summary>
    private static (double X, double Y) Tile(TileType type)
    {
        for (var i = 0; i < _island.Tiles.Length; i++)
        {
            var (x, y) = (i % _island.Width, i / _island.Width);
            if (Enumerable.Range(-1, 3).All(dy => Enumerable.Range(-1, 3).All(dx =>
                _island.InBounds(x + dx, y + dy) && _island.TileAt(x + dx, y + dy) == type)))
            {
                return (x + 0.5, y + 0.5);
            }
        }

        throw new InvalidOperationException($"Na ostrově není {type}.");
    }

    private static (double X, double Y) FreeSpot(Signs signs)
    {
        for (var i = 0; i < _island.Tiles.Length; i++)
        {
            var (x, y) = (i % _island.Width + 0.5, i / _island.Width + 0.5);
            if (_island.Tiles[i] == TileType.Grass && signs.All.All(s => Math.Abs(s.X - x) + Math.Abs(s.Y - y) > 2))
            {
                return (x, y);
            }
        }

        throw new InvalidOperationException("Není volné místo.");
    }
}
