using Oohra.Core.Plants;
using Oohra.Core.Time;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server;

/// <summary>
/// Převod ostrova na to, co smí vidět hráč. Tady se hlídá, aby na klienta nešla skrytá data:
/// z druhu rostliny se posílá jen vzhled, nikdy archetyp ani odchylky složení.
/// </summary>
public static class WorldViews
{
    public static WorldView From(Island island)
    {
        return new WorldView
        {
            Width = island.Width,
            Height = island.Height,
            Tiles = island.Tiles.ToArray(),
            Shore = island.Shore.ToArray(),
            Trees = island.Trees.Select(t => new TreeView(t.TileIndex, (float)t.Variant, t.Kind, (float)t.Height)).ToArray(),
            Plants = island.Plants.Select(p => new PlantView(p.SpeciesId, (float)p.X, (float)p.Y)).ToArray(),
            Species = island.Species
                .Select(s => new SpeciesView((float)s.Hue, (float)s.Saturation, (float)s.Lightness, (short)s.FruitHue, (byte)s.Shape))
                .ToArray(),
            Camps = island.Camps.Select(c => new CampView(c.X, c.Y)).ToArray(),
            Ruins = island.RuinSpots.Select(r => new RuinView((float)r.X, (float)r.Y)).ToArray(),
        };
    }

    /// <summary>Jak rostlina právě vypadá: vykopaná, s plody (podle zralosti), nebo bez nich.</summary>
    public static PlantVisual PlantVisual(Island island, PlantStates states, PlantInstance plant, GameClock clock)
    {
        if (states.IsGone(plant.Id, clock.TotalMinutes))
        {
            return Protocol.PlantVisual.Gone;
        }

        return Gathering.FruitStage(island, states, plant, clock) switch
        {
            Ripeness.Unripe => Protocol.PlantVisual.UnripeFruit,
            Ripeness.Ripe => Protocol.PlantVisual.RipeFruit,
            Ripeness.Overripe => Protocol.PlantVisual.OverripeFruit,
            _ => Protocol.PlantVisual.Plain,
        };
    }
}
