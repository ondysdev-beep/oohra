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
    /// a Sestavit hru pro Windows (unity/Build/Oohra.exe, abys mohl pustit víc oken najednou).
    /// </summary>
    public static class OohraSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Ostrov.unity";

        private static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        [MenuItem("Oohra/Vytvořit scénu ostrova")]
        public static void CreateIslandScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

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

        /// <summary>Sestaví hru pro Windows do unity/Build/Oohra.exe.</summary>
        [MenuItem("Oohra/Sestavit hru pro Windows")]
        public static void BuildWindows()
        {
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
