using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Oohra.Client.Editor
{
    /// <summary>
    /// Menu Oohra v editoru Unity:
    /// Vytvořit scénu ostrova (kamera, slunce, objekt Hra), Spustit server (okno s konzolí serveru)
    /// Sestavit hru pro Windows (unity/Build/Oohra.exe, abys mohl pustit víc oken najednou)
    /// a Katalog vzhledu (vlastní modely stromů, rostlin, staveb a zvířat).
    /// </summary>
    public static class OohraSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Ostrov.unity";

        private static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        [MenuItem("Oohra/Vytvořit scénu ostrova")]
        public static void CreateIslandScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            EnsureMaterials();
            var bootstrap = new GameObject("Hra");
            bootstrap.AddComponent<IslandBootstrap>();

            foreach (var light in Object.FindObjectsByType<Light>())
            {
                if (light.type == LightType.Directional)
                {
                    light.name = "Slunce";
                    RenderSettings.sun = light;
                }
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Oohra: scéna ostrova uložena do " + ScenePath);
        }

        /// <summary>Spustí server v novém okně konzole (dotnet run). Zavřením okna se server vypne.</summary>
        [MenuItem("Oohra/Spustit server")]
        public static void StartServer()
        {
            var project = Path.Combine(RepoRoot, "server", "Oohra.Server");
            Process.Start(new ProcessStartInfo("cmd.exe", "/k dotnet run --project \"" + project + "\"")
            {
                UseShellExecute = true,
                WorkingDirectory = RepoRoot,
            });
            Debug.Log("Oohra: server se spouští v novém okně (první spuštění trvá déle).");
        }

        /// <summary>
        /// Vytvoří sdílené materiály (URP/Lit a URP/Unlit pro plamen) jako soubory v Assets/Resources, pokud chybí.
        /// Jen tak Unity přibalí shadery do sestavené hry (Shader.Find v kódu nestačí).
        /// </summary>
        public static void EnsureMaterials()
        {
            EnsureMaterial(Palette.LitResource, "Universal Render Pipeline/Lit");
            EnsureMaterial(Palette.GlowResource, "Universal Render Pipeline/Unlit");
            EnsureMaterial(Palette.FadeResource, "Universal Render Pipeline/Lit", transparent: true);
            EnsureMaterial(Palette.FadeNormalResource, "Universal Render Pipeline/Lit", transparent: true, normalMap: true);
            EnsureMaterial(Palette.TerrainResource, "Universal Render Pipeline/Terrain/Lit");
            EnsureMaterial(Palette.WaterResource, "Universal Render Pipeline/Lit", transparent: true, water: true);
            EnsureMaterial(Palette.FoamResource, "Universal Render Pipeline/Particles/Simple Lit", transparent: true, foam: true);
            EnsureCatalog();
        }

        /// <summary>Otevře katalog vzhledu v Inspectoru (a vytvoří ho, když chybí). Návod: docs/Grafika.md.</summary>
        [MenuItem("Oohra/Katalog vzhledu")]
        public static void OpenCatalog()
        {
            Selection.activeObject = EnsureCatalog();
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        /// <summary>Soubor katalogu vzhledu v Assets/Resources (prázdný = všude tvary z kódu).</summary>
        private static VisualCatalog EnsureCatalog()
        {
            var path = "Assets/Resources/" + VisualCatalog.Resource + ".asset";
            var catalog = AssetDatabase.LoadAssetAtPath<VisualCatalog>(path);
            if (catalog != null)
            {
                return catalog;
            }

            catalog = ScriptableObject.CreateInstance<VisualCatalog>();
            AssetDatabase.CreateAsset(catalog, path);
            AssetDatabase.SaveAssets();
            Debug.Log("Oohra: katalog vzhledu uložen do " + path);
            return catalog;
        }

        /// <summary>Plochá normálová mapa 4 × 4 (Assets/Resources/OohraFlatNormal.png), vytvoří ji, když chybí.</summary>
        private static Texture2D FlatNormalMap()
        {
            const string path = "Assets/Resources/OohraFlatNormal.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
            {
                var texture = new Texture2D(4, 4);
                var pixels = new Color[16];
                for (var i = 0; i < pixels.Length; i++) pixels[i] = new Color(0.5f, 0.5f, 1f);
                texture.SetPixels(pixels);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void EnsureMaterial(string resource, string shader, bool transparent = false, bool water = false, bool foam = false, bool normalMap = false)
        {
            var path = "Assets/Resources/" + resource + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            var material = new Material(Shader.Find(shader)) { enableInstancing = true };
            if (transparent)
            {
                Palette.MakeTransparent(material);
            }

            if (foam)
            {
                // Pěna: obě strany (na otočení sítě nezáleží), barvu a průhlednost bere i z vrcholů sítě.
                material.SetFloat("_Cull", 0f);
                material.SetColor("_BaseColor", Color.white);
            }

            if (normalMap)
            {
                // Přesně jako průhledná kopie modelu z Meshy za běhu (bez instancingu, s normálovou mapou). Unity bez
                // textury normál slovo _NORMALMAP samo vyhodí, proto dostane plochou normálovou mapu.
                material.enableInstancing = false;
                material.SetTexture("_BumpMap", FlatNormalMap());
                material.EnableKeyword("_NORMALMAP");
            }

            if (water)
            {
                // Hladká lesklá hladina s vlnkami (normálovou mapu dodá hra nebo katalog vzhledu).
                material.SetFloat("_Smoothness", 0.92f);
                material.EnableKeyword("_NORMALMAP");
            }

            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            Debug.Log("Oohra: materiál uložen do " + path);
        }

        /// <summary>
        /// Sestaví hru pro Windows do unity/Build/Oohra.exe. Hra běží v okně (ať jdou dvě vedle sebe)
        /// a i na pozadí, jinak by okno bez fokusu přestalo poslouchat server.
        /// </summary>
        [MenuItem("Oohra/Sestavit hru pro Windows")]
        public static void BuildWindows()
        {
            EnsureMaterials();
            PlayerSettings.productName = "Oohra";
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;

            var output = Path.Combine(Application.dataPath, "..", "Build", "Oohra.exe");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("Oohra: sestavení " + report.summary.result + " → " + Path.GetFullPath(output));
        }
    }
}
