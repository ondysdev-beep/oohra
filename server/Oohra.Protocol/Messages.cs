using LiteNetLib.Utils;
using Oohra.Core;
using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.Plants;
using Oohra.Core.World;

namespace Oohra.Protocol;

// Zprávy a jejich zápis do bajtů. Každá zpráva začíná bajtem MessageType (viz Write... metody).
// Pořadí zápisu a čtení musí být stejné. Když se zpráva změní, zvyš ProtocolInfo.Version.

/// <summary>Strom na mapě: dlaždice a velikost (0 až 1).</summary>
public readonly record struct TreeView(int TileIndex, float Variant);

/// <summary>Rostlina na mapě. Její číslo (id) je pořadí v seznamu.</summary>
public readonly record struct PlantView(int SpeciesId, float X, float Y);

/// <summary>Vzhled druhu rostliny. Žádné složení, jen co hráč vidí.</summary>
public readonly record struct SpeciesView(float Hue, float Saturation, float Lightness, short FruitHue, byte Shape);

public readonly record struct CampView(int X, int Y);

public readonly record struct RuinView(float X, float Y);

/// <summary>Mapa ostrova, jak ji vidí hráč. Posílá se jednou po přihlášení.</summary>
public sealed class WorldView
{
    public int Width { get; init; }

    public int Height { get; init; }

    public TileType[] Tiles { get; init; } = [];

    public bool[] Shore { get; init; } = [];

    public TreeView[] Trees { get; init; } = [];

    public PlantView[] Plants { get; init; } = [];

    public SpeciesView[] Species { get; init; } = [];

    public CampView[] Camps { get; init; } = [];

    public RuinView[] Ruins { get; init; } = [];

    public TileType TileAt(int x, int y)
    {
        return Tiles[y * Width + x];
    }
}

/// <summary>Poloha jednoho hráče ve snímku.</summary>
public readonly record struct PlayerSnapshot(int Id, float X, float Y, float Facing, bool Dead, bool Sleeping);

/// <summary>Snímek světa: čas a polohy všech hráčů. Posílá se mnohokrát za sekundu.</summary>
public sealed record Snapshot(double TotalMinutes, double CalendarMonths, PlayerSnapshot[] Players);

/// <summary>Hráč ve světě (jméno a generace). Posílá se při příchodu a novém narození.</summary>
public sealed record PlayerInfo(int Id, string Nickname, int Generation);

/// <summary>
/// Stav vlastního těla: pruhy, příznaky, spánek a dovednosti. Skryté hodnoty (toxin, horečka) se neposílají.
/// SkillLevels: úroveň každé dovednosti (index = SkillKind), PendingSkills: bity dovedností, jejichž nácvik čeká na spánek.
/// </summary>
public sealed record SelfState(
    float Satiety,
    float Hydration,
    float Energy,
    float Warmth,
    float Health,
    Symptom[] Symptoms,
    int Generation,
    bool Dead,
    DeathCause? DeathCause,
    bool NearFire,
    bool Sheltered,
    bool Sleeping,
    byte[] SkillLevels,
    byte PendingSkills);

/// <summary>Hromádka v batohu. U materiálu (klacek, kámen) se údaje o rostlině nepoužívají.</summary>
public readonly record struct StackView(ItemKind Kind, int SpeciesId, PlantPart Part, Ripeness Ripeness, Cooking Cooking, int Count);

/// <summary>Oheň na ostrově (hoří, nebo z něj zbyl popel).</summary>
public readonly record struct FireView(int Id, float X, float Y, bool Burning);

/// <summary>Stavba hráče (zeď, zeď s otvorem, dveře): střed, úhel v radiánech (0 = na východ, y mapy roste na jih) a délka v metrech.</summary>
public readonly record struct StructureView(int Id, BuildKind Kind, float X, float Y, float Angle, float Length);

public sealed record InventoryView(int Capacity, StackView[] Stacks);

/// <summary>Hláška pro hráče: co se stalo s tělem, se sběrem nebo s pitím. U nové úrovně dovednosti i dovednost a úroveň.</summary>
public sealed record GameEvent(EventKind Kind, byte Code, PlantPart Part, TasteNote[] Notes, byte Skill = 0, byte Level = 0);

public readonly record struct PlantStateChange(int PlantId, PlantVisual Visual);

public sealed record LoginResult(bool Ok, string Reason, int PlayerId);

/// <summary>Zápis a čtení zpráv. Server i klient používají stejné metody.</summary>
public static class Messages
{
    // ---------- klient → server ----------

    public static void WriteLogin(NetDataWriter w, string inviteCode, string nickname)
    {
        w.Put((byte)MessageType.Login);
        w.Put(ProtocolInfo.Version);
        w.Put(inviteCode);
        w.Put(nickname);
    }

    public static (int Version, string InviteCode, string Nickname) ReadLogin(NetDataReader r)
    {
        return (r.GetInt(), r.GetString(64), r.GetString(64));
    }

    /// <summary>Směr chůze v souřadnicích mapy (x doprava, y dolů). (0, 0) = stůj.</summary>
    public static void WriteInput(NetDataWriter w, float dirX, float dirY)
    {
        w.Put((byte)MessageType.Input);
        w.Put(dirX);
        w.Put(dirY);
    }

    public static (float DirX, float DirY) ReadInput(NetDataReader r)
    {
        return (r.GetFloat(), r.GetFloat());
    }

    public static void WriteHarvest(NetDataWriter w, int plantId, PlantPart part)
    {
        w.Put((byte)MessageType.Harvest);
        w.Put(plantId);
        w.Put((byte)part);
    }

    public static (int PlantId, PlantPart Part) ReadHarvest(NetDataReader r)
    {
        return (r.GetInt(), (PlantPart)r.GetByte());
    }

    public static void WriteDrink(NetDataWriter w, int tileX, int tileY)
    {
        w.Put((byte)MessageType.Drink);
        w.Put(tileX);
        w.Put(tileY);
    }

    public static (int TileX, int TileY) ReadDrink(NetDataReader r)
    {
        return (r.GetInt(), r.GetInt());
    }

    /// <summary>Akce s věcí v batohu: Eat, Taste, Drop nebo Bake.</summary>
    public static void WriteStackAction(NetDataWriter w, MessageType type, int stackIndex)
    {
        w.Put((byte)type);
        w.Put(stackIndex);
    }

    public static int ReadStackAction(NetDataReader r)
    {
        return r.GetInt();
    }

    /// <summary>Lehnout si ke spánku, nebo vstát (když postava spí).</summary>
    public static void WriteSleep(NetDataWriter w)
    {
        w.Put((byte)MessageType.Sleep);
    }

    public static void WriteReborn(NetDataWriter w)
    {
        w.Put((byte)MessageType.Reborn);
    }

    /// <summary>Akce s dlaždicí: GatherSticks (strom) nebo TakeStone (skála).</summary>
    public static void WriteTileAction(NetDataWriter w, MessageType type, int tileX, int tileY)
    {
        w.Put((byte)type);
        w.Put(tileX);
        w.Put(tileY);
    }

    public static (int TileX, int TileY) ReadTileAction(NetDataReader r)
    {
        return (r.GetInt(), r.GetInt());
    }

    public static void WriteFeedFire(NetDataWriter w, int fireId)
    {
        w.Put((byte)MessageType.FeedFire);
        w.Put(fireId);
    }

    public static int ReadFeedFire(NetDataReader r)
    {
        return r.GetInt();
    }

    /// <summary>Režim stavby: postavit stavbu se středem (x, y), úhlem v radiánech a délkou (zeď) v metrech.</summary>
    public static void WritePlace(NetDataWriter w, BuildKind kind, float x, float y, float angle, float length)
    {
        w.Put((byte)MessageType.Place);
        w.Put((byte)kind);
        w.Put(x);
        w.Put(y);
        w.Put(angle);
        w.Put(length);
    }

    public static (BuildKind Kind, float X, float Y, float Angle, float Length) ReadPlace(NetDataReader r)
    {
        return ((BuildKind)r.GetByte(), r.GetFloat(), r.GetFloat(), r.GetFloat(), r.GetFloat());
    }

    /// <summary>Rozebrat stavbu podle čísla.</summary>
    public static void WriteRemoveStructure(NetDataWriter w, int structureId)
    {
        w.Put((byte)MessageType.RemoveStructure);
        w.Put(structureId);
    }

    public static int ReadRemoveStructure(NetDataReader r)
    {
        return r.GetInt();
    }

    // ---------- server → klient ----------

    public static void WriteLoginResult(NetDataWriter w, LoginResult result)
    {
        w.Put((byte)MessageType.LoginResult);
        w.Put(result.Ok);
        w.Put(result.Reason);
        w.Put(result.PlayerId);
    }

    public static LoginResult ReadLoginResult(NetDataReader r)
    {
        return new LoginResult(r.GetBool(), r.GetString(), r.GetInt());
    }

    public static void WriteWorld(NetDataWriter w, WorldView world)
    {
        w.Put((byte)MessageType.World);
        w.Put(world.Width);
        w.Put(world.Height);
        w.PutBytesWithLength(world.Tiles.Select(t => (byte)t).ToArray());
        w.PutArray(world.Shore);

        w.Put(world.Trees.Length);
        foreach (var t in world.Trees)
        {
            w.Put(t.TileIndex);
            w.Put(t.Variant);
        }

        w.Put(world.Plants.Length);
        foreach (var p in world.Plants)
        {
            w.Put(p.SpeciesId);
            w.Put(p.X);
            w.Put(p.Y);
        }

        w.Put(world.Species.Length);
        foreach (var s in world.Species)
        {
            w.Put(s.Hue);
            w.Put(s.Saturation);
            w.Put(s.Lightness);
            w.Put(s.FruitHue);
            w.Put(s.Shape);
        }

        w.Put(world.Camps.Length);
        foreach (var c in world.Camps)
        {
            w.Put(c.X);
            w.Put(c.Y);
        }

        w.Put(world.Ruins.Length);
        foreach (var ruin in world.Ruins)
        {
            w.Put(ruin.X);
            w.Put(ruin.Y);
        }
    }

    public static WorldView ReadWorld(NetDataReader r)
    {
        var width = r.GetInt();
        var height = r.GetInt();
        var tiles = r.GetBytesWithLength().Select(b => (TileType)b).ToArray();
        var shore = r.GetBoolArray();
        var trees = ReadList(r, () => new TreeView(r.GetInt(), r.GetFloat()));
        var plants = ReadList(r, () => new PlantView(r.GetInt(), r.GetFloat(), r.GetFloat()));
        var species = ReadList(r, () => new SpeciesView(r.GetFloat(), r.GetFloat(), r.GetFloat(), r.GetShort(), r.GetByte()));
        var camps = ReadList(r, () => new CampView(r.GetInt(), r.GetInt()));
        var ruins = ReadList(r, () => new RuinView(r.GetFloat(), r.GetFloat()));
        return new WorldView
        {
            Width = width,
            Height = height,
            Tiles = tiles,
            Shore = shore,
            Trees = trees,
            Plants = plants,
            Species = species,
            Camps = camps,
            Ruins = ruins,
        };
    }

    public static void WriteSnapshot(NetDataWriter w, Snapshot snapshot)
    {
        w.Put((byte)MessageType.Snapshot);
        w.Put(snapshot.TotalMinutes);
        w.Put(snapshot.CalendarMonths);
        w.Put(snapshot.Players.Length);
        foreach (var p in snapshot.Players)
        {
            w.Put(p.Id);
            w.Put(p.X);
            w.Put(p.Y);
            w.Put(p.Facing);
            w.Put(p.Dead);
            w.Put(p.Sleeping);
        }
    }

    public static Snapshot ReadSnapshot(NetDataReader r)
    {
        var minutes = r.GetDouble();
        var months = r.GetDouble();
        var players = ReadList(r, () => new PlayerSnapshot(r.GetInt(), r.GetFloat(), r.GetFloat(), r.GetFloat(), r.GetBool(), r.GetBool()));
        return new Snapshot(minutes, months, players);
    }

    public static void WritePlayerJoined(NetDataWriter w, PlayerInfo info)
    {
        w.Put((byte)MessageType.PlayerJoined);
        w.Put(info.Id);
        w.Put(info.Nickname);
        w.Put(info.Generation);
    }

    public static PlayerInfo ReadPlayerJoined(NetDataReader r)
    {
        return new PlayerInfo(r.GetInt(), r.GetString(), r.GetInt());
    }

    public static void WritePlayerLeft(NetDataWriter w, int playerId)
    {
        w.Put((byte)MessageType.PlayerLeft);
        w.Put(playerId);
    }

    public static int ReadPlayerLeft(NetDataReader r)
    {
        return r.GetInt();
    }

    public static void WriteSelfState(NetDataWriter w, SelfState s)
    {
        w.Put((byte)MessageType.SelfState);
        w.Put(s.Satiety);
        w.Put(s.Hydration);
        w.Put(s.Energy);
        w.Put(s.Warmth);
        w.Put(s.Health);
        w.PutBytesWithLength(s.Symptoms.Select(x => (byte)x).ToArray());
        w.Put(s.Generation);
        w.Put(s.Dead);
        w.Put(s.DeathCause.HasValue ? (byte)s.DeathCause.Value : byte.MaxValue);
        w.Put(s.NearFire);
        w.Put(s.Sheltered);
        w.Put(s.Sleeping);
        w.PutBytesWithLength(s.SkillLevels);
        w.Put(s.PendingSkills);
    }

    public static SelfState ReadSelfState(NetDataReader r)
    {
        var bars = new[] { r.GetFloat(), r.GetFloat(), r.GetFloat(), r.GetFloat(), r.GetFloat() };
        var symptoms = r.GetBytesWithLength().Select(b => (Symptom)b).ToArray();
        var generation = r.GetInt();
        var dead = r.GetBool();
        var cause = r.GetByte();
        var nearFire = r.GetBool();
        var sheltered = r.GetBool();
        var sleeping = r.GetBool();
        var skillLevels = r.GetBytesWithLength();
        var pendingSkills = r.GetByte();
        return new SelfState(bars[0], bars[1], bars[2], bars[3], bars[4], symptoms, generation, dead,
            cause == byte.MaxValue ? null : (DeathCause)cause, nearFire, sheltered, sleeping, skillLevels, pendingSkills);
    }

    public static void WriteInventory(NetDataWriter w, InventoryView inventory)
    {
        w.Put((byte)MessageType.Inventory);
        w.Put(inventory.Capacity);
        w.Put(inventory.Stacks.Length);
        foreach (var s in inventory.Stacks)
        {
            w.Put((byte)s.Kind);
            w.Put(s.SpeciesId);
            w.Put((byte)s.Part);
            w.Put((byte)s.Ripeness);
            w.Put((byte)s.Cooking);
            w.Put(s.Count);
        }
    }

    public static InventoryView ReadInventory(NetDataReader r)
    {
        var capacity = r.GetInt();
        var stacks = ReadList(r, () => new StackView((ItemKind)r.GetByte(), r.GetInt(), (PlantPart)r.GetByte(), (Ripeness)r.GetByte(), (Cooking)r.GetByte(), r.GetInt()));
        return new InventoryView(capacity, stacks);
    }

    public static void WriteEvent(NetDataWriter w, GameEvent e)
    {
        w.Put((byte)MessageType.Event);
        w.Put((byte)e.Kind);
        w.Put(e.Code);
        w.Put((byte)e.Part);
        w.PutBytesWithLength(e.Notes.Select(n => (byte)n).ToArray());
        w.Put(e.Skill);
        w.Put(e.Level);
    }

    public static GameEvent ReadEvent(NetDataReader r)
    {
        var kind = (EventKind)r.GetByte();
        var code = r.GetByte();
        var part = (PlantPart)r.GetByte();
        var notes = r.GetBytesWithLength().Select(b => (TasteNote)b).ToArray();
        var skill = r.GetByte();
        var level = r.GetByte();
        return new GameEvent(kind, code, part, notes, skill, level);
    }

    public static void WritePlantStates(NetDataWriter w, IReadOnlyList<PlantStateChange> changes)
    {
        w.Put((byte)MessageType.PlantStates);
        w.Put(changes.Count);
        foreach (var c in changes)
        {
            w.Put(c.PlantId);
            w.Put((byte)c.Visual);
        }
    }

    public static PlantStateChange[] ReadPlantStates(NetDataReader r)
    {
        return ReadList(r, () => new PlantStateChange(r.GetInt(), (PlantVisual)r.GetByte()));
    }

    /// <summary>Všechny ohně na ostrově. Posílá se po přihlášení a pak při každé změně.</summary>
    public static void WriteFires(NetDataWriter w, IReadOnlyList<FireView> fires)
    {
        w.Put((byte)MessageType.Fires);
        w.Put(fires.Count);
        foreach (var f in fires)
        {
            w.Put(f.Id);
            w.Put(f.X);
            w.Put(f.Y);
            w.Put(f.Burning);
        }
    }

    public static FireView[] ReadFires(NetDataReader r)
    {
        return ReadList(r, () => new FireView(r.GetInt(), r.GetFloat(), r.GetFloat(), r.GetBool()));
    }

    /// <summary>Všechny stavby hráčů. Posílá se po přihlášení a pak při každé změně.</summary>
    public static void WriteStructures(NetDataWriter w, IReadOnlyList<StructureView> structures)
    {
        w.Put((byte)MessageType.Structures);
        w.Put(structures.Count);
        foreach (var s in structures)
        {
            w.Put(s.Id);
            w.Put((byte)s.Kind);
            w.Put(s.X);
            w.Put(s.Y);
            w.Put(s.Angle);
            w.Put(s.Length);
        }
    }

    public static StructureView[] ReadStructures(NetDataReader r)
    {
        return ReadList(r, () => new StructureView(r.GetInt(), (BuildKind)r.GetByte(), r.GetFloat(), r.GetFloat(), r.GetFloat(), r.GetFloat()));
    }

    private static T[] ReadList<T>(NetDataReader r, Func<T> read)
    {
        var count = r.GetInt();
        var items = new T[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = read();
        }

        return items;
    }
}
