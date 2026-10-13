using System.Text.Json;
using System.Text.Json.Serialization;
using Oohra.Core.Secret;

namespace Oohra.Core.Data;

/// <summary>
/// Načítá datové soubory JSON ze složky Data. Soubory jsou zabalené přímo v knihovně Oohra.Core
/// (EmbeddedResource), takže je není potřeba kopírovat vedle programu.
/// </summary>
public static class GameData
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly Lazy<IReadOnlyList<Archetype>> _archetypes =
        new(() => Load<List<Archetype>>("archetypes.json"));

    private static readonly Lazy<GameSettings> _settings =
        new(() => Load<GameSettings>("settings.json"));

    public static IReadOnlyList<Archetype> Archetypes => _archetypes.Value;

    public static GameSettings Settings => _settings.Value;

    private static T Load<T>(string fileName)
    {
        var resourceName = "Oohra.Core.Data." + fileName;
        using var stream = typeof(GameData).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Datový soubor {fileName} chybí.");

        return JsonSerializer.Deserialize<T>(stream, _options)
            ?? throw new InvalidOperationException($"Datový soubor {fileName} je prázdný.");
    }
}
