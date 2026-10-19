using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Oohra.Client.Editor
{
    /// <summary>
    /// Zařadí nahrávky ze složky Assets/Sounds do katalogu zvuků (Assets/Resources/SoundCatalog.asset) podle
    /// začátku názvu souboru (třeba chopping-wood_02.mp3 přidá druhé seknutí) a nastaví jejich import:
    /// dlouhé smyčky se čtou průběžně z disku (Streaming, nezabírají paměť), krátké zvuky se rozbalí při načtení.
    /// Zvuky z místa (oheň, kroky, sekera…) jsou mono, déšť, bouřka a moře zůstanou stereo. Návod v docs/Zvuky.md.
    /// </summary>
    public static class SoundImport
    {
        public const string Folder = "Assets/Sounds";
        public const string CatalogPath = "Assets/Resources/" + SoundCatalog.Resource + ".asset";

        // Začátek názvu souboru → políčko katalogu.
        private static readonly (string Prefix, string Slot)[] Slots =
        {
            ("fireplace", "FireLoop"),
            ("rain-sound", "RainLoop"),
            ("thunderstorm", "StormLoop"),
            ("waves", "SeaLoop"),
            ("walk-on-grass", "StepsGrass"),
            ("walk-on-sand", "StepsSand"),
            ("walk-on-stone", "StepsStone"),
            ("walk-in-mud", "StepsMud"),
            ("walk-on-wood", "StepsWood"),
            ("chopping-wood", "Chop"),
            ("tree-falls", "TreeFall"),
            ("pickaxe-hit", "TakeStone"),
            ("rock-break", "StoneWork"),
            ("build_wall", "Build"),
            ("stomach-growl", "Stomach"),
        };

        // Smyčky, které zní všude stejně (stereo).
        private static readonly string[] Stereo = { "RainLoop", "StormLoop", "SeaLoop" };

        [MenuItem("Oohra/Zařadit zvuky ze složky Sounds")]
        public static void Run()
        {
            var catalog = EnsureCatalog();
            var found = new Dictionary<string, List<AudioClip>>();
            var paths = AssetDatabase.FindAssets("t:AudioClip", new[] { Folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p, System.StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths)
            {
                var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                var slot = Slots.FirstOrDefault(s => name.StartsWith(s.Prefix)).Slot;
                if (slot == null)
                {
                    Debug.LogWarning("Zvuk " + path + " nemá políčko v katalogu (název nezačíná známým slovem, viz docs/Zvuky.md).");
                    continue;
                }

                SetImport(path, slot.EndsWith("Loop"), !Stereo.Contains(slot));
                if (!found.ContainsKey(slot)) found[slot] = new List<AudioClip>();
                found[slot].Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }

            var serialized = new SerializedObject(catalog);
            foreach (var (_, slot) in Slots)
            {
                List<AudioClip> clips;
                found.TryGetValue(slot, out clips);
                var property = serialized.FindProperty(slot);
                if (property.isArray)
                {
                    property.arraySize = clips == null ? 0 : clips.Count;
                    for (var i = 0; i < property.arraySize; i++)
                    {
                        property.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
                    }
                }
                else
                {
                    property.objectReferenceValue = clips == null ? null : clips[0];
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Selection.activeObject = catalog;
            Debug.Log("Katalog zvuků: zařazeno " + found.Values.Sum(c => c.Count) + " nahrávek.");
        }

        private static void SetImport(string path, bool loop, bool mono)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = loop ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = loop ? 0.5f : 0.7f;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = mono;
            importer.loadInBackground = loop;
            importer.SaveAndReimport();
        }

        private static SoundCatalog EnsureCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SoundCatalog>(CatalogPath);
            if (catalog != null)
            {
                return catalog;
            }

            catalog = ScriptableObject.CreateInstance<SoundCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
        }
    }
}
