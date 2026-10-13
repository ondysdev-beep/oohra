using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Oohra.Client.Tests
{
    /// <summary>
    /// Spustí scénu ostrova a pár snímků nechá hru běžet. Test selže, když se v konzoli objeví chyba.
    /// Uloží i snímek obrazovky do unity/Logs/ostrov.png (Logs se do gitu nenahrává).
    /// </summary>
    public class IslandSceneTests
    {
        [UnityTest]
        public IEnumerator IslandSceneStartsWithoutErrors()
        {
            SceneManager.LoadScene("Ostrov");
            for (var i = 0; i < 30; i++)
            {
                yield return null;
            }

            Assert.IsNotNull(GameObject.Find("Postava"), "Postava ve scéně chybí.");
            Assert.IsNotNull(GameObject.Find("Ostrov (vygenerovaný)"), "Ostrov ve scéně chybí.");
            SaveScreenshot();
        }

        private static void SaveScreenshot()
        {
            var camera = Camera.main;
            var target = new RenderTexture(1280, 720, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            camera.targetTexture = null;
            RenderTexture.active = null;

            var path = Path.Combine(Application.dataPath, "..", "Logs", "ostrov.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log("Snímek uložen: " + Path.GetFullPath(path));
        }
    }
}
