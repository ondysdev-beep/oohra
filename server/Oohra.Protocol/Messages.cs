using LiteNetLib.Utils;
using Oohra.Core;
using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.Language;
using Oohra.Core.Plants;
using Oohra.Core.Social;
using Oohra.Core.World;

namespace Oohra.Protocol;

// Zprávy a jejich zápis do bajtů. Každá zpráva začíná bajtem MessageType (viz Write... metody).
// Pořadí zápisu a čtení musí být stejné. Když se zpráva změní, zvyš ProtocolInfo.Version.

/// <summary>Strom na mapě: dlaždice, varianta modelu (0 až 1), druh a výška v metrech.</summary>
public readonly record struct TreeView(int TileIndex, float Variant, TreeKind Kind = TreeKind.Oak, float Height = 5);

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

/// <summary>Poloha jednoho hráče ve snímku. Torch: drží zapálenou pochodeň, Carry: nese klad, Bag: batoh na zádech.</summary>
public readonly record struct PlayerSnapshot(int Id, float X, float Y, float Facing, bool Dead, bool Sleeping, bool Torch = false, bool Carry = false, ItemKind? Bag = null);

/// <summary>
/// Snímek světa: čas, počasí (druh, síla 0 až 1, vítr 0 až 1) a polohy všech hráčů. Posílá se mnohokrát za sekundu.
/// </summary>
public sealed record Snapshot(
    double TotalMinutes,
    double CalendarMonths,
    PlayerSnapshot[] Players,
    WeatherKind Weather = WeatherKind.Clear,
    float WeatherIntensity = 0,
    float Wind = 0);

/// <summary>Hráč ve světě (jméno a generace). Posílá se při příchodu a novém narození.</summary>
public sealed record PlayerInfo(int Id, string Nickname, int Generation);

/// <summary>
/// Stav vlastního těla: pruhy, příznaky, spánek a dovednosti. Skryté hodnoty (toxin, horečka) se neposílají.
/// SkillLevels: úroveň každé dovednosti (index = SkillKind), PendingSkills: bity dovedností, jejichž nácvik čeká na spánek.
/// WorldGeneration: generace světa (jako jaká generace se teď narodí každý, kdo zemře, návrh 8.2).
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
    byte PendingSkills,
    int WorldGeneration = 1);

/// <summary>Hromádka v batohu. U materiálu (klacek, kámen) se údaje o rostlině nepoužívají. Spoiled: zkažené (zapáchá).</summary>
/// <summary>Hromádka v batohu, jak ji vidí klient. Uses: kolik použití zbývá nástroji, Tree: z jakého stromu je klad.</summary>
public readonly record struct StackView(ItemKind Kind, int SpeciesId, PlantPart Part, Ripeness Ripeness, Cooking Cooking, int Count, bool Spoiled, int Uses = 0, TreeKind Tree = TreeKind.Oak);

/// <summary>
/// Recept, který postava umí (jen ty, neznámé klient nedostane): číslo (Subject v hlášce Craft), id, název,
/// materiál, nástroj (nebo null), kde se dělá a jestli je to oprava.
/// </summary>
public sealed record RecipeView(int Index, string Id, string Name, (ItemKind Kind, int Count)[] Inputs, ItemKind? Tool, bool NearWater, bool NearFire, bool Repair);

/// <summary>Deník, který hráč otevřel: číslo hromádky v batohu a stránky (kresby 12 × 12 bodů).</summary>
public sealed record JournalView(int StackIndex, byte[][] Pages);

/// <summary>
/// Strom, který není celý ze seedu: padá (úhel pádu), pařez, mladý strom (druh, výška, až doroste, růst 0 až 1),
/// nebo dorostlý strom (druh a výška).
/// </summary>
public readonly record struct TreeStateView(int TileIndex, TreeState State, float Angle, TreeKind Kind = TreeKind.Oak, float Height = 0, float Growth = 1);

/// <summary>Dřevo na zemi: kmen nebo klad, druh stromu, střed, směr (radiány) a délka (metry).</summary>
public readonly record struct TimberView(int Id, TimberKind Kind, TreeKind Tree, float X, float Y, float Angle, int Length);

/// <summary>Oheň na ostrově (hoří, nebo z něj zbyl popel).</summary>
public readonly record struct FireView(int Id, float X, float Y, bool Burning);

/// <summary>
/// Stavba hráče (zeď, zeď s otvorem, dveře, past): střed, úhel v radiánech (0 = na východ, y mapy roste na jih)
/// a délka v metrech. Caught: co je v pasti (číslo AnimalKind), -1 = nic.
/// </summary>
/// <summary>Stavba. Decoration: id dekorace (u ostatních staveb prázdné).</summary>
public readonly record struct StructureView(int Id, BuildKind Kind, float X, float Y, float Angle, float Length, int Caught = -1, int OwnerId = -1, string Decoration = "");

/// <summary>Druh dekorace z decorations.json na serveru: název do nabídky, cena, poloměr a jestli se přes ni dá projít.</summary>
public sealed record DecorationTypeView(string Id, string Name, int Sticks, int Stones, float Radius, bool Blocks);

/// <summary>Hromada kamenů na skalní dlaždici: kolik kamenů zbývá (0 = zmizela) a kolik jich má plná.</summary>
public readonly record struct RockPileView(int TileIndex, int Stones, int MaxStones);

/// <summary>
/// Tělo mrtvého hráče: kde leží, kdo to byl (číslo hráče, generace, podpis), kdy zemřel a do kdy smí brát jen
/// jeho kmen (herní minuty), a co u něj zůstalo. Prázdné jméno podpisu = bez podpisu.
/// </summary>
public sealed record CorpseView(
    int Id, float X, float Y, int OwnerId, int Generation, double DiedAt, double TribeOnlyUntil, InventoryView Contents,
    byte[] SignatureName, byte[] SignatureMark);

/// <summary>Zvíře v okolí hráče: poloha, směr pohledu (radiány) a co dělá. Životy se neposílají.</summary>
public readonly record struct AnimalView(int Id, AnimalKind Kind, float X, float Y, float Dir, AnimalState State);

/// <summary>Věci (batoh, zásoby, tělo): počet míst, hromádky a u hráče batoh na zádech (null = jen ruce).</summary>
public sealed record InventoryView(int Capacity, StackView[] Stacks, ItemKind? Bag = null);

/// <summary>Obsah zásob kmene (koše) podle čísla stavby.</summary>
public sealed record StockpileView(int StructureId, InventoryView Contents);

/// <summary>
/// Někdo poblíž řekl slovo (jen slabiky a nálada, význam se nepřenáší). Stranger: mluvil cizinec, PlayerId je pak
/// číslo cizince.
/// </summary>
public sealed record SpeechView(int PlayerId, byte[] Word, Tone Tone, bool Stranger = false);

/// <summary>
/// Někdo poblíž udělal gesto. U ukázání je (X, Y) místo, kam ukazuje. Stranger: cizinec (PlayerId = jeho číslo).
/// Item: věc, kterou ukazuje (gesto ShowItem), jinak null.
/// </summary>
public readonly record struct GestureView(int PlayerId, Gesture Gesture, float X, float Y, bool Stranger = false, StackView? Item = null);

/// <summary>Cizinec z cizího kmene (NPC): kmen 0 nebo 1, poloha, směr pohledu a co dělá.</summary>
public readonly record struct StrangerView(int Id, int Tribe, float X, float Y, float Facing, StrangerState State);

/// <summary>Slovo v zápisníku hráče: kolikrát zaznělo, v jakých situacích (index SpeechContext) a jaký význam mu hráč dal.</summary>
public sealed record WordEntryView(byte[] Word, int Heard, int[] Contexts, Concept? Meaning, bool OldVoice);

/// <summary>Celý zápisník slov a naposledy slyšené slovo (prázdné = žádné).</summary>
public sealed record VocabularyView(WordEntryView[] Entries, byte[] LastHeard);

/// <summary>
/// Značka ve světě: kresba 12 × 12 (SignBits), kdo ji nakreslil (-1 = stará rytina), kdy (herní minuty) a kdy zvětrá
/// (-1 = rytina, nezvětrá). Stará rytina má záznam: druh rostliny, jméno ze slabik a poznámky (RecordSpeciesId -1 = bez záznamu).
/// </summary>
public sealed record SignView(
    int Id, float X, float Y, byte[] Bits, int AuthorId, double CreatedAt, double WeathersAt, bool Carved, bool Ancient,
    int RecordSpeciesId, byte[] RecordName, CarvingTag[] RecordTags, byte[]? SignatureName = null, byte[]? SignatureMark = null, bool ByAncient = false);

/// <summary>
/// Hláška pro hráče: co se stalo s tělem, se sběrem, s pitím, s lovem... U nové úrovně dovednosti nese dovednost
/// a úroveň, u lovu druh zvířete (Subject).
/// </summary>
public sealed record GameEvent(EventKind Kind, byte Code, PlantPart Part, TasteNote[] Notes, byte Skill = 0, byte Level = 0, byte Subject = 0);

public readonly record struct PlantStateChange(int PlantId, PlantVisual Visual);

/// <summary>Výsledek přihlášení. NewCharacter: hráč přišel poprvé (klient mu pustí sen bez slov, návrh 4.2).</summary>
public sealed record LoginResult(bool Ok, string Reason, int PlayerId, bool NewCharacter = false);

/// <summary>Zápis a čtení zpráv. Server i klient používají stejné metody.</summary>
public static class Messages
{
    /// <summary>Nejdelší id dekorace (ochrana proti nesmyslným zprávám).</summary>
    public const int MaxDecorationId = 64;

    // ---------- klient → server ----------

    /// <summary>Přihlášení: kód pozvánky, přezdívka a tajný klíč hráče (server si u přezdívky pamatuje jeho otisk).</summary>
    public static void WriteLogin(NetDataWriter w, string inviteCode, string nickname, string playerKey)
    {
        w.Put((byte)MessageType.Login);
        w.Put(ProtocolInfo.Version);
        w.Put(inviteCode);
        w.Put(nickname);
        w.Put(playerKey);
    }

    public static (int Version, string InviteCode, string Nickname, string PlayerKey) ReadLogin(NetDataReader r)
    {
        return (r.GetInt(), r.GetString(64), r.GetString(64), r.GetString(128));
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

    /// <summary>Lov: Attack (zaútočit), Butcher (stáhnout maso) na zvíře podle čísla, nebo TakeCatch (úlovek z pasti podle čísla stavby).</summary>
    public static void WriteAnimalAction(NetDataWriter w, MessageType type, int animalId)
    {
        w.Put((byte)type);
        w.Put(animalId);
    }

    public static int ReadAnimalAction(NetDataReader r)
    {
        return r.GetInt();
    }

    /// <summary>Vyrobit kopí z klacku a kamene.</summary>
    public static void WriteMakeSpear(NetDataWriter w)
    {
        w.Put((byte)MessageType.MakeSpear);
    }

    /// <summary>Vyrobit kamennou sekeru (klacek a kameny).</summary>
    public static void WriteMakeAxe(NetDataWriter w)
    {
        w.Put((byte)MessageType.MakeAxe);
    }

    /// <summary>Vyrobit podle receptu (id z crafting.json).</summary>
    public static void WriteCraft(NetDataWriter w, string recipeId)
    {
        w.Put((byte)MessageType.Craft);
        w.Put(recipeId);
    }

    public static string ReadCraft(NetDataReader r)
    {
        return r.GetString(64);
    }

    /// <summary>Zkusit spojit vybrané hromádky z batohu (najde recept, který z nich jde).</summary>
    public static void WriteCombine(NetDataWriter w, IReadOnlyList<int> stackIndices)
    {
        w.Put((byte)MessageType.Combine);
        w.Put((byte)stackIndices.Count);
        foreach (var index in stackIndices)
        {
            w.Put((byte)index);
        }
    }

    public static int[] ReadCombine(NetDataReader r)
    {
        var count = Math.Min((int)r.GetByte(), 8);
        var indices = new int[count];
        for (var i = 0; i < count; i++)
        {
            indices[i] = r.GetByte();
        }

        return indices;
    }

    /// <summary>Recepty, které postava umí. Posílá se po přihlášení a když se nějaký naučí (nebo zemře).</summary>
    public static void WriteRecipes(NetDataWriter w, IReadOnlyList<RecipeView> recipes)
    {
        w.Put((byte)MessageType.Recipes);
        w.Put(recipes.Count);
        foreach (var recipe in recipes)
        {
            w.Put(recipe.Index);
            w.Put(recipe.Id);
            w.Put(recipe.Name);
            w.Put((byte)recipe.Inputs.Length);
            foreach (var (kind, count) in recipe.Inputs)
            {
                w.Put((byte)kind);
                w.Put((byte)count);
            }

            w.Put(recipe.Tool.HasValue ? (byte)recipe.Tool.Value : (byte)255);
            w.Put(recipe.NearWater);
            w.Put(recipe.NearFire);
            w.Put(recipe.Repair);
        }
    }

    public static RecipeView[] ReadRecipes(NetDataReader r)
    {
        return ReadList(r, () =>
        {
            var (index, id, name) = (r.GetInt(), r.GetString(64), r.GetString(128));
            var inputs = new (ItemKind, int)[r.GetByte()];
            for (var i = 0; i < inputs.Length; i++)
            {
                inputs[i] = ((ItemKind)r.GetByte(), r.GetByte());
            }

            var tool = r.GetByte();
            return new RecipeView(index, id, name, inputs, tool == 255 ? null : (ItemKind)tool, r.GetBool(), r.GetBool(), r.GetBool());
        });
    }

    /// <summary>Ukázat hráči (číslo), jak se dělá recept.</summary>
    public static void WriteTeachRecipe(NetDataWriter w, int playerId, string recipeId)
    {
        w.Put((byte)MessageType.TeachRecipe);
        w.Put(playerId);
        w.Put(recipeId);
    }

    public static (int PlayerId, string RecipeId) ReadTeachRecipe(NetDataReader r)
    {
        return (r.GetInt(), r.GetString(64));
    }

    /// <summary>Nakreslit stránku deníku z hromádky v batohu.</summary>
    public static void WriteDrawJournal(NetDataWriter w, int stackIndex, int page, byte[] bits)
    {
        w.Put((byte)MessageType.DrawJournal);
        w.Put((byte)stackIndex);
        w.Put((byte)page);
        w.Put(bits);
    }

    public static (int StackIndex, int Page, byte[] Bits) ReadDrawJournal(NetDataReader r)
    {
        var (stack, page) = (r.GetByte(), r.GetByte());
        var bits = new byte[SignBits.ByteCount];
        r.GetBytes(bits, SignBits.ByteCount);
        return (stack, page, bits);
    }

    /// <summary>Obsah deníku pro hráče, který ho drží.</summary>
    public static void WriteJournal(NetDataWriter w, JournalView journal)
    {
        w.Put((byte)MessageType.Journal);
        w.Put((byte)journal.StackIndex);
        w.Put((byte)journal.Pages.Length);
        foreach (var page in journal.Pages)
        {
            w.Put(page);
        }
    }

    public static JournalView ReadJournal(NetDataReader r)
    {
        var stack = r.GetByte();
        var pages = new byte[r.GetByte()][];
        for (var i = 0; i < pages.Length; i++)
        {
            pages[i] = new byte[SignBits.ByteCount];
            r.GetBytes(pages[i], SignBits.ByteCount);
        }

        return new JournalView(stack, pages);
    }

    /// <summary>Natřít se bahnem (u bažiny nebo vody) nebo popelem z batohu, ať komáři neštípou.</summary>
    public static void WriteSmear(NetDataWriter w, bool ash)
    {
        w.Put((byte)MessageType.Smear);
        w.Put(ash);
    }

    public static bool ReadSmear(NetDataReader r)
    {
        return r.GetBool();
    }

    /// <summary>Sundat pijavice (u ohně nebo popelem).</summary>
    public static void WriteRemoveLeeches(NetDataWriter w)
    {
        w.Put((byte)MessageType.RemoveLeeches);
    }

    /// <summary>Sundat batoh ze zad do rukou.</summary>
    public static void WriteTakeOffBag(NetDataWriter w)
    {
        w.Put((byte)MessageType.TakeOffBag);
    }

    /// <summary>Položit nesený klad.</summary>
    public static void WriteDropTimber(NetDataWriter w)
    {
        w.Put((byte)MessageType.DropTimber);
    }

    /// <summary>Akce s dřevem na zemi: ChopTimber (odseknout klad, rozštípat klad) nebo PickUpTimber.</summary>
    public static void WriteTimberAction(NetDataWriter w, MessageType type, int timberId)
    {
        w.Put((byte)type);
        w.Put(timberId);
    }

    public static int ReadTimberAction(NetDataReader r)
    {
        return r.GetInt();
    }

    /// <summary>Lehnout si ke spánku, nebo vstát (když postava spí).</summary>
    public static void WriteSleep(NetDataWriter w)
    {
        w.Put((byte)MessageType.Sleep);
    }

    /// <summary>Narodit se znovu: v divočině, nebo u svého kmene.</summary>
    public static void WriteReborn(NetDataWriter w, BirthPlace place)
    {
        w.Put((byte)MessageType.Reborn);
        w.Put((byte)place);
    }

    /// <summary>Neznámá hodnota (jiný klient) znamená divočinu.</summary>
    public static BirthPlace ReadReborn(NetDataReader r)
    {
        var place = (BirthPlace)r.GetByte();
        return Enum.IsDefined(typeof(BirthPlace), place) ? place : BirthPlace.Wild;
    }

    /// <summary>Akce s dlaždicí: GatherSticks a ChopTree (strom), TakeStone (skála) nebo Fish (voda).</summary>
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
    public static void WritePlace(NetDataWriter w, BuildKind kind, float x, float y, float angle, float length, string decoration = "")
    {
        w.Put((byte)MessageType.Place);
        w.Put((byte)kind);
        w.Put(x);
        w.Put(y);
        w.Put(angle);
        w.Put(length);
        w.Put(decoration ?? string.Empty);
    }

    /// <summary>Decoration: id dekorace z decorations.json (u ostatních staveb prázdné).</summary>
    public static (BuildKind Kind, float X, float Y, float Angle, float Length, string Decoration) ReadPlace(NetDataReader r)
    {
        return ((BuildKind)r.GetByte(), r.GetFloat(), r.GetFloat(), r.GetFloat(), r.GetFloat(), r.GetString(MaxDecorationId));
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

    /// <summary>Říct slovo (1 až 3 slabiky) s náladou.</summary>
    public static void WriteSay(NetDataWriter w, byte[] word, Tone tone)
    {
        w.Put((byte)MessageType.Say);
        PutBytes(w, word);
        w.Put((byte)tone);
    }

    public static (byte[] Word, Tone Tone) ReadSay(NetDataReader r)
    {
        return (GetBytes(r), (Tone)r.GetByte());
    }

    /// <summary>Udělat gesto. U ukázání je (x, y) místo na mapě, jinak se nepoužije.</summary>
    public static void WriteGesture(NetDataWriter w, Gesture gesture, float x, float y)
    {
        w.Put((byte)MessageType.Gesture);
        w.Put((byte)gesture);
        w.Put(x);
        w.Put(y);
    }

    public static (Gesture Gesture, float X, float Y) ReadGesture(NetDataReader r)
    {
        return ((Gesture)r.GetByte(), r.GetFloat(), r.GetFloat());
    }

    /// <summary>Zapsat si do zápisníku význam slova (null = smazat).</summary>
    public static void WriteSetMeaning(NetDataWriter w, byte[] word, Concept? meaning)
    {
        w.Put((byte)MessageType.SetMeaning);
        PutBytes(w, word);
        PutConcept(w, meaning);
    }

    public static (byte[] Word, Concept? Meaning) ReadSetMeaning(NetDataReader r)
    {
        return (GetBytes(r), GetConcept(r));
    }

    /// <summary>Nakreslit značku (kresba SignBits, 18 bajtů) na místo (x, y) kousek od postavy.</summary>
    public static void WriteDrawSign(NetDataWriter w, float x, float y, byte[] bits)
    {
        w.Put((byte)MessageType.DrawSign);
        w.Put(x);
        w.Put(y);
        PutBytes(w, bits);
    }

    public static (float X, float Y, byte[] Bits) ReadDrawSign(NetDataReader r)
    {
        return (r.GetFloat(), r.GetFloat(), GetBytes(r));
    }

    /// <summary>Smazat vlastní značku.</summary>
    public static void WriteEraseSign(NetDataWriter w, int signId)
    {
        w.Put((byte)MessageType.EraseSign);
        w.Put(signId);
    }

    public static int ReadEraseSign(NetDataReader r)
    {
        return r.GetInt();
    }

    /// <summary>Pozvat hráče (podle čísla) do kmene. Pozvaný musí stát kousek od tebe.</summary>
    public static void WriteInviteToTribe(NetDataWriter w, int playerId)
    {
        w.Put((byte)MessageType.InviteToTribe);
        w.Put(playerId);
    }

    public static int ReadInviteToTribe(NetDataReader r)
    {
        return r.GetInt();
    }

    /// <summary>Odpovědět na pozvánku hráče inviterId.</summary>
    public static void WriteAnswerInvite(NetDataWriter w, int inviterId, bool accept)
    {
        w.Put((byte)MessageType.AnswerInvite);
        w.Put(inviterId);
        w.Put(accept);
    }

    public static (int InviterId, bool Accept) ReadAnswerInvite(NetDataReader r)
    {
        return (r.GetInt(), r.GetBool());
    }

    public static void WriteLeaveTribe(NetDataWriter w)
    {
        w.Put((byte)MessageType.LeaveTribe);
    }

    /// <summary>Uložit hromádku z batohu do zásob (StoreItem), nebo vzít hromádku ze zásob (TakeItem).</summary>
    public static void WriteStockAction(NetDataWriter w, MessageType type, int stockpileId, int stackIndex)
    {
        w.Put((byte)type);
        w.Put(stockpileId);
        w.Put(stackIndex);
    }

    public static (int StockpileId, int StackIndex) ReadStockAction(NetDataReader r)
    {
        return (r.GetInt(), r.GetInt());
    }

    // ---------- server → klient ----------

    /// <summary>Dekorace, které jde postavit (ze serveru, decorations.json). Posílá se po přihlášení.</summary>
    public static void WriteDecorationTypes(NetDataWriter w, IReadOnlyList<DecorationTypeView> decorations)
    {
        w.Put((byte)MessageType.DecorationTypes);
        w.Put(decorations.Count);
        foreach (var d in decorations)
        {
            w.Put(d.Id);
            w.Put(d.Name);
            w.Put(d.Sticks);
            w.Put(d.Stones);
            w.Put(d.Radius);
            w.Put(d.Blocks);
        }
    }

    public static DecorationTypeView[] ReadDecorationTypes(NetDataReader r)
    {
        return ReadList(r, () => new DecorationTypeView(r.GetString(MaxDecorationId), r.GetString(), r.GetInt(), r.GetInt(), r.GetFloat(), r.GetBool()));
    }

    /// <summary>Nastavit si podpis: jméno ze slabik (1 až 3) a znak (SignBits).</summary>
    public static void WriteSetSignature(NetDataWriter w, byte[] name, byte[] mark)
    {
        w.Put((byte)MessageType.SetSignature);
        PutBytes(w, name);
        PutBytes(w, mark);
    }

    public static (byte[] Name, byte[] Mark) ReadSetSignature(NetDataReader r)
    {
        return (GetBytes(r), GetBytes(r));
    }

    /// <summary>Nahlásit hráče, značku (targetId = jejich číslo) nebo obecný problém (targetId se nepoužije).</summary>
    public static void WriteReport(NetDataWriter w, ReportTarget target, int targetId, ReportReason reason)
    {
        w.Put((byte)MessageType.Report);
        w.Put((byte)target);
        w.Put(targetId);
        w.Put((byte)reason);
    }

    public static (ReportTarget Target, int TargetId, ReportReason Reason) ReadReport(NetDataReader r)
    {
        return ((ReportTarget)r.GetByte(), r.GetInt(), (ReportReason)r.GetByte());
    }

    /// <summary>Můj podpis (prázdné jméno = ještě žádný). Posílá se po přihlášení a po změně.</summary>
    public static void WriteSignature(NetDataWriter w, byte[] name, byte[] mark)
    {
        w.Put((byte)MessageType.Signature);
        PutBytes(w, name);
        PutBytes(w, mark);
    }

    public static (byte[] Name, byte[] Mark) ReadSignature(NetDataReader r)
    {
        return (GetBytes(r), GetBytes(r));
    }

    /// <summary>Těla mrtvých hráčů i s věcmi. Posílá se po přihlášení a pak při každé změně.</summary>
    public static void WriteCorpses(NetDataWriter w, IReadOnlyList<CorpseView> corpses)
    {
        w.Put((byte)MessageType.Corpses);
        w.Put(corpses.Count);
        foreach (var c in corpses)
        {
            w.Put(c.Id);
            w.Put(c.X);
            w.Put(c.Y);
            w.Put(c.OwnerId);
            w.Put(c.Generation);
            w.Put(c.DiedAt);
            w.Put(c.TribeOnlyUntil);
            PutInventory(w, c.Contents);
            PutBytes(w, c.SignatureName);
            PutBytes(w, c.SignatureMark);
        }
    }

    public static CorpseView[] ReadCorpses(NetDataReader r)
    {
        return ReadList(r, () => new CorpseView(
            r.GetInt(), r.GetFloat(), r.GetFloat(), r.GetInt(), r.GetInt(), r.GetDouble(), r.GetDouble(), GetInventory(r), GetBytes(r), GetBytes(r)));
    }

    /// <summary>Hromady kamenů na skalách. Posílá se po přihlášení a pak při každé změně.</summary>
    public static void WriteRockPiles(NetDataWriter w, IReadOnlyList<RockPileView> piles)
    {
        w.Put((byte)MessageType.RockPiles);
        w.Put(piles.Count);
        foreach (var p in piles)
        {
            w.Put(p.TileIndex);
            w.Put((byte)p.Stones);
            w.Put((byte)p.MaxStones);
        }
    }

    public static RockPileView[] ReadRockPiles(NetDataReader r)
    {
        return ReadList(r, () => new RockPileView(r.GetInt(), r.GetByte(), r.GetByte()));
    }

    /// <summary>Hráč inviterId tě zve do svého kmene.</summary>
    public static void WriteTribeInvite(NetDataWriter w, int inviterId)
    {
        w.Put((byte)MessageType.TribeInvite);
        w.Put(inviterId);
    }

    public static int ReadTribeInvite(NetDataReader r)
    {
        return r.GetInt();
    }

    /// <summary>Kdo je s tebou v kmeni (čísla hráčů včetně tebe, prázdné = nejsi v kmeni).</summary>
    public static void WriteTribe(NetDataWriter w, IReadOnlyList<int> members)
    {
        w.Put((byte)MessageType.Tribe);
        w.Put(members.Count);
        foreach (var id in members)
        {
            w.Put(id);
        }
    }

    public static int[] ReadTribe(NetDataReader r)
    {
        return ReadList(r, r.GetInt);
    }

    /// <summary>Všechny značky. Posílá se po přihlášení a pak při každé změně.</summary>
    public static void WriteSigns(NetDataWriter w, IReadOnlyList<SignView> signs)
    {
        w.Put((byte)MessageType.Signs);
        w.Put(signs.Count);
        foreach (var s in signs)
        {
            w.Put(s.Id);
            w.Put(s.X);
            w.Put(s.Y);
            PutBytes(w, s.Bits);
            w.Put(s.AuthorId);
            w.Put(s.CreatedAt);
            w.Put(s.WeathersAt);
            w.Put(s.Carved);
            w.Put(s.Ancient);
            w.Put(s.RecordSpeciesId);
            PutBytes(w, s.RecordName);
            PutBytes(w, s.RecordTags.Select(t => (byte)t).ToArray());
            PutBytes(w, s.SignatureName ?? []);
            PutBytes(w, s.SignatureMark ?? []);
            w.Put(s.ByAncient);
        }
    }

    public static SignView[] ReadSigns(NetDataReader r)
    {
        return ReadList(r, () => new SignView(
            r.GetInt(), r.GetFloat(), r.GetFloat(), GetBytes(r), r.GetInt(), r.GetDouble(), r.GetDouble(), r.GetBool(), r.GetBool(),
            r.GetInt(), GetBytes(r), GetBytes(r).Select(b => (CarvingTag)b).ToArray(), GetBytes(r), GetBytes(r), r.GetBool()));
    }

    private static void PutBytes(NetDataWriter w, byte[] bytes)
    {
        w.Put((byte)bytes.Length);
        w.Put(bytes);
    }

    private static byte[] GetBytes(NetDataReader r)
    {
        var bytes = new byte[r.GetByte()];
        r.GetBytes(bytes, bytes.Length);
        return bytes;
    }

    public static void WriteSpeech(NetDataWriter w, SpeechView speech)
    {
        w.Put((byte)MessageType.Speech);
        w.Put(speech.PlayerId);
        PutBytes(w, speech.Word);
        w.Put((byte)speech.Tone);
        w.Put(speech.Stranger);
    }

    public static SpeechView ReadSpeech(NetDataReader r)
    {
        return new SpeechView(r.GetInt(), GetBytes(r), (Tone)r.GetByte(), r.GetBool());
    }

    public static void WriteGestureSeen(NetDataWriter w, GestureView gesture)
    {
        w.Put((byte)MessageType.GestureSeen);
        w.Put(gesture.PlayerId);
        w.Put((byte)gesture.Gesture);
        w.Put(gesture.X);
        w.Put(gesture.Y);
        w.Put(gesture.Stranger);
        w.Put(gesture.Item.HasValue);
        if (gesture.Item is { } item)
        {
            PutStack(w, item);
        }
    }

    public static GestureView ReadGestureSeen(NetDataReader r)
    {
        var (playerId, gesture, x, y, stranger) = (r.GetInt(), (Gesture)r.GetByte(), r.GetFloat(), r.GetFloat(), r.GetBool());
        StackView? item = r.GetBool() ? GetStack(r) : null;
        return new GestureView(playerId, gesture, x, y, stranger, item);
    }

    /// <summary>Sen bez slov skončil (nebo ho hráč přeskočil): postava se probudí a začne žít.</summary>
    public static void WriteDreamOver(NetDataWriter w)
    {
        w.Put((byte)MessageType.DreamOver);
    }

    /// <summary>Zapálit pochodeň od ohně poblíž (klacek z batohu).</summary>
    public static void WriteLightTorch(NetDataWriter w)
    {
        w.Put((byte)MessageType.LightTorch);
    }

    /// <summary>Ukázat ostatním věc z hromádky v batohu (gesto ukázat předmět, návrh 9.3).</summary>
    public static void WriteShowItem(NetDataWriter w, int stackIndex)
    {
        w.Put((byte)MessageType.ShowItem);
        w.Put(stackIndex);
    }

    public static int ReadShowItem(NetDataReader r)
    {
        return r.GetInt();
    }

    /// <summary>Zápisník slov. Posílá se po přihlášení a pak při každé změně.</summary>
    public static void WriteVocabulary(NetDataWriter w, VocabularyView vocabulary)
    {
        w.Put((byte)MessageType.Vocabulary);
        w.Put(vocabulary.Entries.Length);
        foreach (var e in vocabulary.Entries)
        {
            PutBytes(w, e.Word);
            w.Put(e.Heard);
            w.Put((byte)e.Contexts.Length);
            foreach (var count in e.Contexts)
            {
                w.Put(count);
            }

            PutConcept(w, e.Meaning);
            w.Put(e.OldVoice);
        }

        PutBytes(w, vocabulary.LastHeard);
    }

    public static VocabularyView ReadVocabulary(NetDataReader r)
    {
        var entries = ReadList(r, () =>
        {
            var word = GetBytes(r);
            var heard = r.GetInt();
            var contexts = new int[r.GetByte()];
            for (var i = 0; i < contexts.Length; i++)
            {
                contexts[i] = r.GetInt();
            }

            return new WordEntryView(word, heard, contexts, GetConcept(r), r.GetBool());
        });
        return new VocabularyView(entries, GetBytes(r));
    }

    /// <summary>Význam slova jako bajt, 255 = žádný.</summary>
    private static void PutConcept(NetDataWriter w, Concept? concept)
    {
        w.Put(concept.HasValue ? (byte)concept.Value : byte.MaxValue);
    }

    private static Concept? GetConcept(NetDataReader r)
    {
        var value = r.GetByte();
        return value == byte.MaxValue ? null : (Concept)value;
    }

    public static void WriteLoginResult(NetDataWriter w, LoginResult result)
    {
        w.Put((byte)MessageType.LoginResult);
        w.Put(result.Ok);
        w.Put(result.Reason);
        w.Put(result.PlayerId);
        w.Put(result.NewCharacter);
    }

    public static LoginResult ReadLoginResult(NetDataReader r)
    {
        return new LoginResult(r.GetBool(), r.GetString(), r.GetInt(), r.GetBool());
    }

    public static void WriteWorld(NetDataWriter w, WorldView world)
    {
        w.Put((byte)MessageType.World);
        w.Put(world.Width);
        w.Put(world.Height);
        PutTiles(w, world.Tiles, world.Shore);

        w.Put(world.Trees.Length);
        foreach (var t in world.Trees)
        {
            w.Put(t.TileIndex);
            w.Put(t.Variant);
            w.Put((byte)t.Kind);
            // Výška v decimetrech (stromy mají 3 až 8 m), stačí jeden bajt.
            w.Put((byte)Math.Round(t.Height * 10));
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

    // Bit pobřeží v bajtu dlaždice (druhů dlaždic je méně než 128).
    private const byte ShoreBit = 0x80;

    /// <summary>
    /// Dlaždice a pobřeží v jednom bajtu na dlaždici, bez délky na začátku (tu dá šířka × výška).
    /// PutBytesWithLength a PutArray z LiteNetLib zapisují délku jen do 2 bajtů (nejvýš 65 535), mapa 384 × 384 je větší.
    /// </summary>
    private static void PutTiles(NetDataWriter w, TileType[] tiles, bool[] shore)
    {
        var bytes = new byte[tiles.Length];
        for (var i = 0; i < tiles.Length; i++)
        {
            bytes[i] = (byte)((byte)tiles[i] | (shore[i] ? ShoreBit : 0));
        }

        w.Put(bytes);
    }

    private static (TileType[] Tiles, bool[] Shore) GetTiles(NetDataReader r, int count)
    {
        var bytes = new byte[count];
        r.GetBytes(bytes, count);
        var tiles = new TileType[count];
        var shore = new bool[count];
        for (var i = 0; i < count; i++)
        {
            tiles[i] = (TileType)(bytes[i] & ~ShoreBit);
            shore[i] = (bytes[i] & ShoreBit) != 0;
        }

        return (tiles, shore);
    }

    public static WorldView ReadWorld(NetDataReader r)
    {
        var width = r.GetInt();
        var height = r.GetInt();
        var (tiles, shore) = GetTiles(r, width * height);
        var trees = ReadList(r, () => new TreeView(r.GetInt(), r.GetFloat(), (TreeKind)r.GetByte(), r.GetByte() / 10f));
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
        w.Put((byte)snapshot.Weather);
        w.Put(snapshot.WeatherIntensity);
        w.Put(snapshot.Wind);
        w.Put(snapshot.Players.Length);
        foreach (var p in snapshot.Players)
        {
            w.Put(p.Id);
            w.Put(p.X);
            w.Put(p.Y);
            w.Put(p.Facing);
            w.Put(p.Dead);
            w.Put(p.Sleeping);
            w.Put(p.Torch);
            w.Put(p.Carry);
            w.Put(p.Bag.HasValue ? (byte)p.Bag.Value : (byte)255);
        }
    }

    private static ItemKind? BagOf(byte value)
    {
        return value == 255 ? null : (ItemKind)value;
    }

    public static Snapshot ReadSnapshot(NetDataReader r)
    {
        var minutes = r.GetDouble();
        var months = r.GetDouble();
        var (weather, intensity, wind) = ((WeatherKind)r.GetByte(), r.GetFloat(), r.GetFloat());
        var players = ReadList(r, () => new PlayerSnapshot(r.GetInt(), r.GetFloat(), r.GetFloat(), r.GetFloat(), r.GetBool(), r.GetBool(), r.GetBool(), r.GetBool(), BagOf(r.GetByte())));
        return new Snapshot(minutes, months, players, weather, intensity, wind);
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
        w.Put(s.WorldGeneration);
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
        var worldGeneration = r.GetInt();
        return new SelfState(bars[0], bars[1], bars[2], bars[3], bars[4], symptoms, generation, dead,
            cause == byte.MaxValue ? null : (DeathCause)cause, nearFire, sheltered, sleeping, skillLevels, pendingSkills, worldGeneration);
    }

    public static void WriteInventory(NetDataWriter w, InventoryView inventory)
    {
        w.Put((byte)MessageType.Inventory);
        PutInventory(w, inventory);
    }

    public static InventoryView ReadInventory(NetDataReader r)
    {
        return GetInventory(r);
    }

    /// <summary>Zásoby kmene, do kterých hráč smí (jeho a jeho kmene). Posílá se při každé změně.</summary>
    public static void WriteStockpiles(NetDataWriter w, IReadOnlyList<StockpileView> stockpiles)
    {
        w.Put((byte)MessageType.Stockpiles);
        w.Put(stockpiles.Count);
        foreach (var s in stockpiles)
        {
            w.Put(s.StructureId);
            PutInventory(w, s.Contents);
        }
    }

    public static StockpileView[] ReadStockpiles(NetDataReader r)
    {
        return ReadList(r, () => new StockpileView(r.GetInt(), GetInventory(r)));
    }

    private static void PutInventory(NetDataWriter w, InventoryView inventory)
    {
        w.Put(inventory.Capacity);
        w.Put(inventory.Stacks.Length);
        foreach (var s in inventory.Stacks)
        {
            PutStack(w, s);
        }

        w.Put(inventory.Bag.HasValue ? (byte)inventory.Bag.Value : (byte)255);
    }

    private static InventoryView GetInventory(NetDataReader r)
    {
        var capacity = r.GetInt();
        var stacks = ReadList(r, () => GetStack(r));
        var bag = r.GetByte();
        return new InventoryView(capacity, stacks, bag == 255 ? null : (ItemKind)bag);
    }

    private static void PutStack(NetDataWriter w, StackView s)
    {
        w.Put((byte)s.Kind);
        w.Put(s.SpeciesId);
        w.Put((byte)s.Part);
        w.Put((byte)s.Ripeness);
        w.Put((byte)s.Cooking);
        w.Put(s.Count);
        w.Put(s.Spoiled);
        w.Put((short)s.Uses);
        w.Put((byte)s.Tree);
    }

    private static StackView GetStack(NetDataReader r)
    {
        return new StackView((ItemKind)r.GetByte(), r.GetInt(), (PlantPart)r.GetByte(), (Ripeness)r.GetByte(), (Cooking)r.GetByte(), r.GetInt(), r.GetBool(), r.GetShort(), (TreeKind)r.GetByte());
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
        w.Put(e.Subject);
    }

    public static GameEvent ReadEvent(NetDataReader r)
    {
        var kind = (EventKind)r.GetByte();
        var code = r.GetByte();
        var part = (PlantPart)r.GetByte();
        var notes = r.GetBytesWithLength().Select(b => (TasteNote)b).ToArray();
        var skill = r.GetByte();
        var level = r.GetByte();
        var subject = r.GetByte();
        return new GameEvent(kind, code, part, notes, skill, level, subject);
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

    /// <summary>Stromy, které nestojí celé (padají nebo pařezy). Posílá se po přihlášení a při každé změně.</summary>
    public static void WriteTreeStates(NetDataWriter w, IReadOnlyList<TreeStateView> trees)
    {
        w.Put((byte)MessageType.TreeStates);
        w.Put(trees.Count);
        foreach (var t in trees)
        {
            w.Put(t.TileIndex);
            w.Put((byte)t.State);
            w.Put(t.Angle);
            w.Put((byte)t.Kind);
            w.Put((byte)Math.Round(t.Height * 10));
            w.Put((byte)Math.Round(t.Growth * 100));
        }
    }

    public static TreeStateView[] ReadTreeStates(NetDataReader r)
    {
        return ReadList(r, () => new TreeStateView(r.GetInt(), (TreeState)r.GetByte(), r.GetFloat(), (TreeKind)r.GetByte(), r.GetByte() / 10f, r.GetByte() / 100f));
    }

    /// <summary>Všechno dřevo na zemi (kmeny a klady). Posílá se po přihlášení a při každé změně.</summary>
    public static void WriteTimbers(NetDataWriter w, IReadOnlyList<TimberView> timbers)
    {
        w.Put((byte)MessageType.Timbers);
        w.Put(timbers.Count);
        foreach (var t in timbers)
        {
            w.Put(t.Id);
            w.Put((byte)t.Kind);
            w.Put((byte)t.Tree);
            w.Put(t.X);
            w.Put(t.Y);
            w.Put(t.Angle);
            w.Put((byte)t.Length);
        }
    }

    public static TimberView[] ReadTimbers(NetDataReader r)
    {
        return ReadList(r, () => new TimberView(r.GetInt(), (TimberKind)r.GetByte(), (TreeKind)r.GetByte(), r.GetFloat(), r.GetFloat(), r.GetFloat(), r.GetByte()));
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
            w.Put(s.Caught);
            w.Put(s.OwnerId);
            w.Put(s.Decoration ?? string.Empty);
        }
    }

    public static StructureView[] ReadStructures(NetDataReader r)
    {
        return ReadList(r, () => new StructureView(r.GetInt(), (BuildKind)r.GetByte(), r.GetFloat(), r.GetFloat(), r.GetFloat(), r.GetFloat(), r.GetInt(), r.GetInt(), r.GetString(MaxDecorationId)));
    }

    /// <summary>Zvířata v okolí hráče. Posílá se často (jako snímek hráčů), každému jen jeho okolí.</summary>
    /// <summary>Velikost zprávy se zvířaty: druh zprávy a počet, pak jedno zvíře (id, druh, x, y, směr, stav).</summary>
    public const int AnimalsHeaderBytes = 1 + 4;

    public const int AnimalViewBytes = 4 + 1 + 4 + 4 + 4 + 1;

    public static void WriteAnimals(NetDataWriter w, IReadOnlyList<AnimalView> animals)
    {
        w.Put((byte)MessageType.Animals);
        w.Put(animals.Count);
        foreach (var a in animals)
        {
            w.Put(a.Id);
            w.Put((byte)a.Kind);
            w.Put(a.X);
            w.Put(a.Y);
            w.Put(a.Dir);
            w.Put((byte)a.State);
        }
    }

    public static AnimalView[] ReadAnimals(NetDataReader r)
    {
        return ReadList(r, () => new AnimalView(r.GetInt(), (AnimalKind)r.GetByte(), r.GetFloat(), r.GetFloat(), r.GetFloat(), (AnimalState)r.GetByte()));
    }

    /// <summary>Oznámení od správce serveru (třeba „za 5 minut restart“). Hráči ho uvidí v hláškách.</summary>
    public static void WriteAnnouncement(NetDataWriter w, string text)
    {
        w.Put((byte)MessageType.Announcement);
        w.Put(text);
    }

    public static string ReadAnnouncement(NetDataReader r)
    {
        return r.GetString(400);
    }

    /// <summary>Cizinci v okolí hráče (posílá se jako zvířata, často a bez opakování).</summary>
    public static void WriteStrangers(NetDataWriter w, IReadOnlyList<StrangerView> strangers)
    {
        w.Put((byte)MessageType.Strangers);
        w.Put(strangers.Count);
        foreach (var s in strangers)
        {
            w.Put(s.Id);
            w.Put((byte)s.Tribe);
            w.Put(s.X);
            w.Put(s.Y);
            w.Put(s.Facing);
            w.Put((byte)s.State);
        }
    }

    public static StrangerView[] ReadStrangers(NetDataReader r)
    {
        return ReadList(r, () => new StrangerView(r.GetInt(), r.GetByte(), r.GetFloat(), r.GetFloat(), r.GetFloat(), (StrangerState)r.GetByte()));
    }

    /// <summary>Dát cizinci jednu věc z hromádky v batohu.</summary>
    public static void WriteGiveToStranger(NetDataWriter w, int strangerId, int stackIndex)
    {
        w.Put((byte)MessageType.GiveToStranger);
        w.Put(strangerId);
        w.Put(stackIndex);
    }

    public static (int StrangerId, int StackIndex) ReadGiveToStranger(NetDataReader r)
    {
        return (r.GetInt(), r.GetInt());
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
