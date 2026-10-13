using System.Collections;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Oohra.Client.Tests
{
    /// <summary>
    /// Pomůcky pro testy PlayMode: skutečný server (Oohra.Server z server/, musí být sestavený přes dotnet build),
    /// čekání na podmínku a snímek z kamery do unity/Logs (do gitu se nenahrávají).
    /// </summary>
    public static class PlayTestTools
    {
        public const string Invite = "UNITY-TEST";

        /// <summary>Spustí server. extraJson jsou další položky server.json, např. "\"startHour\": 22".</summary>
        public static Process StartServer(int port, string extraJson = "")
        {
            var repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var serverDll = Path.Combine(repo, "server", "Oohra.Server", "bin", "Debug", "net10.0", "Oohra.Server.dll");
            Assert.IsTrue(File.Exists(serverDll), "Server není sestavený. Spusť dotnet build ve složce server.");

            var config = Path.Combine(Application.temporaryCachePath, "server-test-" + port + ".json");
            var extra = extraJson.Length > 0 ? ", " + extraJson : string.Empty;
            File.WriteAllText(config, "{ \"port\": " + port + ", \"seed\": 1, \"worldSize\": 84, \"inviteCodes\": [\"" + Invite + "\"]" + extra + " }");
            return Process.Start(new ProcessStartInfo("dotnet", "\"" + serverDll + "\" \"" + config + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }

        public static void StopServer(Process server)
        {
            if (server != null && !server.HasExited)
            {
                server.Kill();
            }
        }

        public static IEnumerator WaitFor(System.Func<bool> condition, float seconds, string message)
        {
            var until = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > until)
                {
                    Assert.Fail(message);
                }

                yield return null;
            }
        }

        /// <summary>
        /// Snímek z kamery (bez rozhraní OnGUI). WaitForEndOfFrame ani ScreenCapture v Unity bez okna
        /// (batch mode) nefungují, proto se kreslí přímo kamerou do textury.
        /// </summary>
        public static IEnumerator SaveScreenshot(string fileName)
        {
            yield return null;
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

            var path = Path.Combine(Application.dataPath, "..", "Logs", fileName);
            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log("Snímek uložen: " + Path.GetFullPath(path));
        }
    }
}
