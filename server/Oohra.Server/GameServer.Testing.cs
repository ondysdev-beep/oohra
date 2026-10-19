using System.Globalization;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Time;
using Oohra.Core.World;

namespace Oohra.Server;

/// <summary>
/// Příkazy správce na rychlé zkoušení (píšou se do okna serveru, hráči je napsat nemůžou): dát věc, posunout čas
/// a měsíc, nastavit tělo, uzdravit, přenést postavu na místo, naučit všechny recepty, rozdělat oheň.
/// </summary>
public sealed partial class GameServer
{
    public const string TestingHelp =
        "Zkoušení: dat <přezdívka> <věc> [počet] | cas <hodina 0-23> | mesic <1-12> | telo <přezdívka> <sytost|voda|energie|teplo|zdravi> <0-100> "
        + "| uzdravit <přezdívka> | presun <přezdívka> <more|melcina|bazina|mocal|skala|vrchovina|les|louka|voda|tabor|ruiny|x y> | recepty <přezdívka> | ohen <přezdívka>";

    /// <summary>Názvy věcí v příkazu dat (bez háčků a čárek).</summary>
    private static readonly Dictionary<string, ItemKind> ItemNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["klacek"] = ItemKind.Stick,
        ["kamen"] = ItemKind.Stone,
        ["pazourek"] = ItemKind.Flint,
        ["lyko"] = ItemKind.Bast,
        ["kost"] = ItemKind.Bone,
        ["kuze"] = ItemKind.Hide,
        ["vycinena"] = ItemKind.TannedHide,
        ["popel"] = ItemKind.Ash,
        ["drevo"] = ItemKind.Wood,
        ["trisky"] = ItemKind.Splinter,
        ["maso"] = ItemKind.Meat,
        ["ryba"] = ItemKind.Fish,
        ["sekera"] = ItemKind.Axe,
        ["nuz"] = ItemKind.Knife,
        ["kopi"] = ItemKind.Spear,
        ["jehla"] = ItemKind.Needle,
        ["hacek"] = ItemKind.Hook,
        ["batoh"] = ItemKind.SmallBag,
        ["velkybatoh"] = ItemKind.LargeBag,
        ["denik"] = ItemKind.Journal,
    };

    /// <summary>Příkazy na zkoušení, nebo null (není to příkaz na zkoušení).</summary>
    private string? RunTestingCommand(string command, string argument)
    {
        var args = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return command switch
        {
            "dat" or "give" => Give(args),
            "cas" or "time" => SetHour(args),
            "mesic" or "month" => SetMonth(args),
            "telo" or "body" => SetBody(args),
            "uzdravit" or "heal" => Heal(args),
            "presun" or "tp" => Teleport(args),
            "recepty" or "recipes" => LearnAll(args),
            "ohen" or "fire" => LightFireFor(args),
            _ => null,
        };
    }

    /// <summary>Dá hráči věci. Co se mu nevejde do rukou a batohu, položí se na zem vedle něj.</summary>
    private string Give(string[] args)
    {
        if (args.Length < 2 || FindPlayer(args[0]) is not { } player)
        {
            return "Použití: dat <přezdívka> <věc> [počet]. Věci: " + string.Join(", ", ItemNames.Keys);
        }

        if (!ItemNames.TryGetValue(args[1], out var kind))
        {
            return $"Neznám věc {args[1]}. Věci: " + string.Join(", ", ItemNames.Keys);
        }

        var count = args.Length > 2 && int.TryParse(args[2], out var n) ? Math.Clamp(n, 1, 99) : 1;
        var onGround = 0;
        for (var i = 0; i < count; i++)
        {
            var item = NewItem(kind);
            if (!player.Inventory.TryAdd(item))
            {
                Island.Ground.Put(Island, item, 1, player.X, player.Y, player.Level, player.Facing, Clock.TotalMinutes);
                onGround++;
            }
        }

        SendInventory(player);
        var ground = onGround > 0 ? $" ({onGround} na zemi vedle, do rukou se nevešly)" : string.Empty;
        return $"{player.Nickname} dostal {args[1]} ×{count}{ground}.";
    }

    /// <summary>Nová věc daného druhu: maso, ryba a kůže s dnešním dnem, nástroj s použitími podle receptu.</summary>
    private Item NewItem(ItemKind kind)
    {
        var uses = GameData.Crafting.Recipes.FirstOrDefault(r => r.Output == kind && r.Uses > 0)?.Uses ?? 0;
        return kind switch
        {
            ItemKind.Meat => Item.RawMeat(Clock.Day),
            ItemKind.Fish => Item.RawFish(Clock.Day),
            ItemKind.Hide => new Item(ItemKind.Hide, Day: Clock.Day),
            ItemKind.Journal => Journals.Create(),
            _ => new Item(kind, Uses: uses),
        };
    }

    /// <summary>Posune čas dopředu na danou hodinu (dnes, nebo zítra, když už byla). Zpátky čas nejde.</summary>
    private string SetHour(string[] args)
    {
        if (args.Length < 1 || !double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var hour) || hour < 0 || hour >= 24)
        {
            return $"Použití: cas <hodina 0-23>, třeba cas 21. Teď je {Clock.Hour:0.0} h.";
        }

        var today = Clock.Day * GameClock.MinutesPerDay;
        var target = today + hour * 60;
        if (target < Clock.TotalMinutes)
        {
            target += GameClock.MinutesPerDay;
        }

        Clock.Restore(target, Clock.CalendarMonths);
        return $"Je {hour:0.#} h (den {Clock.Day}).";
    }

    /// <summary>Nastaví měsíc roku (zima, jarní záplava, hadi a vlci podle ročního období).</summary>
    private string SetMonth(string[] args)
    {
        var months = GameData.Settings.MonthsPerYear;
        if (args.Length < 1 || !int.TryParse(args[0], out var month) || month < 1 || month > months)
        {
            return $"Použití: mesic <1-{months}>. Teď je měsíc {Clock.MonthOfYear + 1} ({Clock.Season}).";
        }

        Clock.Restore(Clock.TotalMinutes, Clock.Year * months + month - 1 + 0.5);
        return $"Je měsíc {month} ({Clock.Season}).";
    }

    /// <summary>Nastaví hodnotu těla (sytost, voda, energie, teplo, zdraví).</summary>
    private string SetBody(string[] args)
    {
        if (args.Length < 3 || FindPlayer(args[0]) is not { } player
            || !double.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return "Použití: telo <přezdívka> <sytost|voda|energie|teplo|zdravi> <0-100>, třeba telo Ondra sytost 20";
        }

        var c = player.Character;
        value = Math.Clamp(value, 0, 100);
        switch (args[1].ToLowerInvariant())
        {
            case "sytost": c.Satiety = value; break;
            case "voda": c.Hydration = value; break;
            case "energie": c.Energy = value; break;
            case "teplo": c.Warmth = value; break;
            case "zdravi": c.Health = Math.Max(1, value); break;
            default: return "Neznám hodnotu " + args[1] + ". Jde: sytost, voda, energie, teplo, zdravi.";
        }

        SendSelfState(player);
        return $"{player.Nickname}: {args[1]} {value:0}.";
    }

    /// <summary>Plné tělo, žádný jed, pijavice ani klíšťata. Nemoci zůstanou (jsou skryté, léčí se lékem).</summary>
    private string Heal(string[] args)
    {
        if (args.Length < 1 || FindPlayer(args[0]) is not { } player)
        {
            return "Použití: uzdravit <přezdívka>";
        }

        if (player.Character.IsDead)
        {
            return $"{player.Nickname} je mrtvý (narodí se znovu sám tlačítkem ve hře).";
        }

        var c = player.Character;
        (c.Satiety, c.Hydration, c.Energy, c.Warmth, c.Health) = (100, 100, 100, 70, 100);
        c.Toxin = 0;
        c.Leeches = 0;
        SendSelfState(player);
        return $"{player.Nickname} je najedený, napitý, odpočatý a zdravý.";
    }

    /// <summary>Přenese hráče na nejbližší místo daného druhu (nebo na souřadnice x y).</summary>
    private string Teleport(string[] args)
    {
        if (args.Length < 2 || FindPlayer(args[0]) is not { } player)
        {
            return "Použití: presun <přezdívka> <more|melcina|bazina|mocal|skala|vrchovina|les|louka|voda|tabor|ruiny|x y>";
        }

        Position? spot;
        if (args.Length >= 3 && double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            && double.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            spot = Nearest(player, (tx, ty) => Math.Abs(tx + 0.5 - x) < 3 && Math.Abs(ty + 0.5 - y) < 3);
        }
        else
        {
            var place = PlaceTest(args[1].ToLowerInvariant());
            if (place is null)
            {
                return "Neznám místo " + args[1] + ". Jde: more, melcina, bazina, mocal, skala, vrchovina, les, louka, voda, tabor, ruiny, nebo x y.";
            }

            spot = Nearest(player, place);
        }

        if (spot is not { } to)
        {
            return "Takové místo, kde se dá stát, jsem nenašel.";
        }

        (player.X, player.Y) = (to.X, to.Y);
        player.Level = 0;
        player.InputX = player.InputY = 0;
        return $"{player.Nickname} je na X {to.X:0} Y {to.Y:0}.";
    }

    /// <summary>Jaké dlaždice patří k místu (souš, kde se dá stát, vybere Nearest).</summary>
    private Func<int, int, bool>? PlaceTest(string place)
    {
        var camps = Island.Camps;
        var rocks = Island.Rocks.All.Where(p => p.Stones > 0).ToList();
        return place switch
        {
            "more" or "plaz" => (x, y) => Island.TileAt(x, y) == TileType.Sand && Near(x, y, 2, TileType.Sea),
            "bazina" => (x, y) => Island.TileAt(x, y) == TileType.Bog,
            "mocal" => (x, y) => Island.TileAt(x, y) == TileType.Mire,
            "melcina" => (x, y) => Island.TileAt(x, y) == TileType.Shallows,
            "skala" or "kameny" => (x, y) => rocks.Any(p => Math.Abs(p.X - x - 0.5) <= 1.5 && Math.Abs(p.Y - y - 0.5) <= 1.5),
            "vrchovina" => (x, y) => Island.TileAt(x, y) == TileType.Highland,
            "les" => (x, y) => Island.TileAt(x, y) == TileType.Forest,
            "louka" => (x, y) => Island.TileAt(x, y) == TileType.Grass && Island.RegionAt(x, y) == RegionKind.Meadow,
            "voda" or "reka" => (x, y) => Island.TileAt(x, y) is TileType.Grass or TileType.Sand && Near(x, y, 1, TileType.Pond),
            "tabor" => (x, y) => camps.Any(c => Math.Abs(c.X - x) <= 4 && Math.Abs(c.Y - y) <= 4),
            "ruiny" => (x, y) => Island.RuinSpots.Any(r => Math.Abs(r.X - x - 0.5) <= 3 && Math.Abs(r.Y - y - 0.5) <= 3),
            _ => null,
        };
    }

    /// <summary>Je do vzdálenosti range (dlaždice) dlaždice daného druhu?</summary>
    private bool Near(int x, int y, int range, TileType tile)
    {
        for (var dy = -range; dy <= range; dy++)
        {
            for (var dx = -range; dx <= range; dx++)
            {
                if (Island.InBounds(x + dx, y + dy) && Island.TileAt(x + dx, y + dy) == tile)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Nejbližší dlaždice ostrova (ne jeskyně), která vyhovuje a kde se dá stát.</summary>
    private Position? Nearest(PlayerSession player, Func<int, int, bool> wanted)
    {
        Position? best = null;
        var bestDistance = double.MaxValue;
        var radius = GameData.Movement.Radius;
        for (var y = 0; y < Island.SurfaceHeight; y++)
        {
            for (var x = 0; x < Island.Width; x++)
            {
                var distance = (x + 0.5 - player.X) * (x + 0.5 - player.X) + (y + 0.5 - player.Y) * (y + 0.5 - player.Y);
                if (distance >= bestDistance || !Movement.CanStand(Island, x + 0.5, y + 0.5, radius) || !wanted(x, y))
                {
                    continue;
                }

                bestDistance = distance;
                best = new Position(x + 0.5, y + 0.5);
            }
        }

        return best;
    }

    /// <summary>Postava umí všechny recepty (smrtí je zase zapomene).</summary>
    private string LearnAll(string[] args)
    {
        if (args.Length < 1 || FindPlayer(args[0]) is not { } player)
        {
            return "Použití: recepty <přezdívka>";
        }

        foreach (var recipe in GameData.Crafting.Recipes)
        {
            player.Character.Learn(recipe.Id);
        }

        return $"{player.Nickname} umí všechny recepty ({GameData.Crafting.Recipes.Count}).";
    }

    /// <summary>Rozdělá zadarmo oheň kousek před hráčem.</summary>
    private string LightFireFor(string[] args)
    {
        if (args.Length < 1 || FindPlayer(args[0]) is not { } player)
        {
            return "Použití: ohen <přezdívka>";
        }

        // Materiál na oheň (klacky a kameny) dá server sám, hráči nic neubude.
        var materials = new Inventory(20);
        for (var i = 0; i < Fires.FireSticks(null) + 2; i++)
        {
            materials.TryAdd(Item.Stick);
        }

        for (var i = 0; i < GameData.Fire.Fire.StonesCost; i++)
        {
            materials.TryAdd(Item.Stone);
        }

        var (x, y) = (player.X + Math.Cos(player.Facing) * 1.2, player.Y + Math.Sin(player.Facing) * 1.2);
        var result = Fires.Light(Island, materials, player.X, player.Y, x, y);
        return result == FireResult.Lit ? $"Oheň hoří u {player.Nickname}." : $"Oheň se nerozdělal ({result}), zkus jiné místo.";
    }
}
