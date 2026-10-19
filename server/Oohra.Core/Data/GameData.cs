using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oohra.Core.Secret;

namespace Oohra.Core.Data;

/// <summary>
/// Načítá datové soubory JSON ze složky Data. Soubory jsou zabalené přímo v knihovně Oohra.Core
/// (EmbeddedResource), takže je není potřeba kopírovat vedle programu.
/// Komentáře v JSON (// ...) jsou povolené, velikost písmen v názvech se nerozlišuje.
/// </summary>
public static class GameData
{
    private static readonly JsonSerializer _serializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        Converters = { new StringEnumConverter() },
        MissingMemberHandling = MissingMemberHandling.Error,
    });

    private static readonly Lazy<IReadOnlyList<Archetype>> _archetypes =
        new(() => Load<List<Archetype>>("archetypes.json"));

    private static readonly Lazy<GameSettings> _settings =
        new(() => Load<GameSettings>("settings.json"));

    private static readonly Lazy<PlantRules> _plantRules =
        new(() => Load<PlantRules>("plant_rules.json"));

    private static readonly Lazy<BodyRules> _body =
        new(() => Load<BodyRules>("body.json"));

    private static readonly Lazy<GenerationRules> _generations =
        new(() => Load<GenerationRules>("generations.json"));

    private static readonly Lazy<MovementRules> _movement =
        new(() => Load<MovementRules>("movement.json"));

    private static readonly Lazy<GatheringRules> _gathering =
        new(() => Load<GatheringRules>("gathering.json"));

    private static readonly Lazy<FireRules> _fire =
        new(() => Load<FireRules>("fire.json"));

    public static IReadOnlyList<Archetype> Archetypes => _archetypes.Value;

    public static FireRules Fire => _fire.Value;

    private static readonly Lazy<BuildingRules> _building =
        new(() => Load<BuildingRules>("building.json"));

    public static BuildingRules Building => _building.Value;

    private static readonly Lazy<SkillRules> _skills =
        new(() => Load<SkillRules>("skills.json"));

    public static SkillRules Skills => _skills.Value;

    private static readonly Lazy<AnimalRules> _animals =
        new(() => Load<AnimalRules>("animals.json"));

    public static AnimalRules Animals => _animals.Value;

    private static readonly Lazy<LanguageRules> _language =
        new(() => Load<LanguageRules>("language.json"));

    public static LanguageRules Language => _language.Value;

    private static readonly Lazy<SignRules> _signs =
        new(() => Load<SignRules>("signs.json"));

    public static SignRules Signs => _signs.Value;

    private static readonly Lazy<TribeRules> _tribes =
        new(() => Load<TribeRules>("tribes.json"));

    public static TribeRules Tribes => _tribes.Value;

    private static readonly Lazy<RockRules> _rocks =
        new(() => Load<RockRules>("rocks.json"));

    public static RockRules Rocks => _rocks.Value;

    private static readonly Lazy<CorpseRules> _corpses =
        new(() => Load<CorpseRules>("corpses.json"));

    public static CorpseRules Corpses => _corpses.Value;

    private static readonly Lazy<WeatherRules> _weather =
        new(() => Load<WeatherRules>("weather.json"));

    public static WeatherRules Weather => _weather.Value;

    private static readonly Lazy<WorldGenRules> _worldGen =
        new(() => Load<WorldGenRules>("worldgen.json"));

    public static WorldGenRules WorldGen => _worldGen.Value;

    private static readonly Lazy<TreeRules> _trees =
        new(() => Load<TreeRules>("trees.json"));

    public static TreeRules Trees => _trees.Value;

    private static readonly Lazy<WoodRules> _wood =
        new(() => Load<WoodRules>("woodcutting.json"));

    public static WoodRules Wood => _wood.Value;

    private static readonly Lazy<CraftingRules> _crafting =
        new(() => Load<CraftingRules>("crafting.json"));

    public static CraftingRules Crafting => _crafting.Value;

    private static readonly Lazy<SeasonRules> _seasons =
        new(() => Load<SeasonRules>("seasons.json"));

    public static SeasonRules Seasons => _seasons.Value;

    private static readonly Lazy<HazardRules> _hazards =
        new(() => Load<HazardRules>("hazards.json"));

    public static HazardRules Hazards => _hazards.Value;

    private static readonly Lazy<StrangerRules> _strangers =
        new(() => Load<StrangerRules>("strangers.json"));

    public static StrangerRules Strangers => _strangers.Value;

    private static readonly Lazy<IReadOnlyList<DecorationType>> _decorations =
        new(() => Load<List<DecorationType>>("decorations.json"));

    /// <summary>Dekorace pro režim stavby (pořadí jako v souboru).</summary>
    public static IReadOnlyList<DecorationType> Decorations => _decorations.Value;

    public static DecorationType? Decoration(string? id)
    {
        return Decorations.FirstOrDefault(d => d.Id == id);
    }

    public static GatheringRules Gathering => _gathering.Value;

    public static MovementRules Movement => _movement.Value;

    public static BodyRules Body => _body.Value;

    public static GenerationRules Generations => _generations.Value;

    public static PlantRules PlantRules => _plantRules.Value;

    public static GameSettings Settings => _settings.Value;

    private static T Load<T>(string fileName)
    {
        var resourceName = "Oohra.Core.Data." + fileName;
        using var stream = typeof(GameData).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Datový soubor {fileName} chybí.");
        using var reader = new StreamReader(stream);
        using var json = new JsonTextReader(reader);

        return _serializer.Deserialize<T>(json)
            ?? throw new InvalidOperationException($"Datový soubor {fileName} je prázdný.");
    }
}
