using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Oohra.Client.Editor
{
    /// <summary>
    /// Založí scénu ostrova: kamera, slunce a objekt IslandBootstrap, který při spuštění postaví zbytek.
    /// Menu: Oohra → Vytvořit scénu ostrova. Jde spustit i z příkazové řádky (-executeMethod).
    /// </summary>
    public static class OohraSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Ostrov.unity";

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
    }
}
