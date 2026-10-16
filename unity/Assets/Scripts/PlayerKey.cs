using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Tajný klíč hráče: vytvoří se při prvním spuštění hry a uloží na tomto počítači (PlayerPrefs). Posílá se při
    /// přihlášení. Server postavu pustí jen s klíčem, se kterým vznikla, takže za cizí přezdívku hrát nejde.
    /// Na jiném počítači se klíč dá zadat parametrem -key (nebo správce postavu odemkne příkazem unlock).
    /// </summary>
    public static class PlayerKey
    {
        private const string PrefsKey = "Oohra.PlayerKey";

        public static string Current()
        {
            var fromCommandLine = CommandLineKey();
            if (!string.IsNullOrEmpty(fromCommandLine))
            {
                return fromCommandLine;
            }

            var key = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (key.Length == 0)
            {
                key = System.Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(PrefsKey, key);
                PlayerPrefs.Save();
            }

            return key;
        }

        private static string CommandLineKey()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-key")
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
