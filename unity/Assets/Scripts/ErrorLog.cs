using System;
using System.IO;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Záznam chyb hry u testera: každá chyba (výjimka, chybová hláška) se zapíše do souboru chyby.txt v datové složce
    /// hry (Application.persistentDataPath, cestu ukáže F1). Tester ho pošle správci. Soubor nepřeroste 1 MB:
    /// když je větší, přejmenuje se na chyby-starsi.txt a začne se nový. V editoru Unity se nezapisuje.
    /// </summary>
    public static class ErrorLog
    {
        private const long MaxBytes = 1024 * 1024;
        private static string _path;

        /// <summary>Kde soubor s chybami je (pro F1 a testery).</summary>
        public static string FilePath => _path ?? Path.Combine(Application.persistentDataPath, "chyby.txt");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Start()
        {
            if (Application.isEditor)
            {
                return;
            }

            Application.logMessageReceived += OnLog;
        }

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert)
            {
                Write(FilePath, type + ": " + message, stackTrace);
            }
        }

        /// <summary>Zapíše jednu chybu s časem a verzí hry. Když se zapsat nedá, nic se neděje (hra hraje dál).</summary>
        public static void Write(string path, string message, string stackTrace)
        {
            try
            {
                var file = new FileInfo(path);
                if (file.Exists && file.Length > MaxBytes)
                {
                    var older = Path.Combine(file.DirectoryName ?? ".", "chyby-starsi.txt");
                    File.Copy(path, older, overwrite: true);
                    File.WriteAllText(path, string.Empty);
                }

                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] verze {Application.version}, protokol {Oohra.Protocol.ProtocolInfo.Version}\n{message}\n{stackTrace}\n");
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
