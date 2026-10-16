using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Spouští hru: ukáže přihlášení (server, kód pozvánky, přezdívka), připojí se k serveru,
    /// a když server pošle mapu, postaví ostrov, hráče, kameru a rozhraní.
    /// Pravidla počítá server. Klient nemá Oohra.Core, a tedy ani skryté složení rostlin.
    /// </summary>
    public class IslandBootstrap : MonoBehaviour
    {
        private const float ConnectTimeoutSeconds = 6f;
        private const string NicknameKey = "oohra.nickname";
        private const string HostKey = "oohra.host";

        [SerializeField] private string _host = "127.0.0.1";
        [SerializeField] private int _port = ProtocolInfo.DefaultPort;
        [SerializeField] private string _inviteCode = "OOHRA-TEST";
        [SerializeField] private string _nickname = string.Empty;

        private GameConnection _connection;
        private ClientSession _session;
        private PlayerViews _players;
        private CameraRig _cameraRig;
        private string _status = string.Empty;
        private float _connectStarted;
        private bool _worldBuilt;
        private GUIStyle _label;
        private GUIStyle _title;

        /// <summary>Hra po přihlášení (pro testy a ladění), jinak null.</summary>
        public ClientSession Session => _worldBuilt ? _session : null;

        private void Awake()
        {
            _nickname = PlayerPrefs.GetString(NicknameKey, _nickname);
            _host = PlayerPrefs.GetString(HostKey, _host);
        }

        /// <summary>
        /// Přihlášení z příkazové řádky, např. Oohra.exe -nick Petr (volitelně -server, -port, -code, -key).
        /// Hodí se pro spuštění dvou oken bez vyplňování a pro ověření sestavené hry.
        /// </summary>
        private void Start()
        {
            var nickname = CommandLineValue("-nick");
            if (nickname == null)
            {
                return;
            }

            int port;
            Connect(
                CommandLineValue("-server") ?? _host,
                int.TryParse(CommandLineValue("-port"), out port) ? port : _port,
                CommandLineValue("-code") ?? _inviteCode,
                nickname);
        }

        private static string CommandLineValue(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        /// <summary>Připojí se k serveru a přihlásí se. Volá ho tlačítko Připojit (a testy).</summary>
        public void Connect(string host, int port, string inviteCode, string nickname)
        {
            Disconnect();
            _host = host;
            _port = port;
            _inviteCode = inviteCode;
            _nickname = nickname;
            PlayerPrefs.SetString(NicknameKey, nickname);
            PlayerPrefs.SetString(HostKey, host);

            _connection = new GameConnection();
            _connection.Connect(host, port, inviteCode, nickname, PlayerKey.Current());
            _session = new ClientSession(_connection);
            _connectStarted = Time.unscaledTime;
            _status = "Připojuji se k serveru…";
        }

        private void Update()
        {
            if (_session == null)
            {
                return;
            }

            _session.Update();
            if (!_worldBuilt)
            {
                CheckLogin();
                return;
            }

            if (_cameraRig == null && _players.Self != null && !DreamView.Active)
            {
                _cameraRig = Camera.main.gameObject.AddComponent<CameraRig>();
                _cameraRig.Init(_players.Self);
            }

            if (_connection.Disconnected)
            {
                _status = "Spojení se serverem se přerušilo.";
            }
        }

        private void CheckLogin()
        {
            if (_connection.LoginError.Length > 0)
            {
                _status = _connection.LoginError;
                Disconnect();
            }
            else if (_connection.LoggedIn && _connection.World != null && _connection.Self != null)
            {
                BuildWorld();
            }
            else if (_connection.Disconnected || Time.unscaledTime - _connectStarted > ConnectTimeoutSeconds)
            {
                _status = "Server neodpovídá. Běží? (V Unity: menu Oohra → Spustit server.)";
                Disconnect();
            }
        }

        /// <summary>Postaví ostrov. Když se to nepovede, ukáže chybu místo nekonečného přihlašování.</summary>
        private void BuildWorld()
        {
            try
            {
                BuildScene();
            }
            catch (System.Exception error)
            {
                Debug.LogException(error);
                _status = "Ostrov se nepodařilo postavit: " + error.Message;
                Disconnect();
                return;
            }

            _worldBuilt = true;
            _status = string.Empty;
            Debug.Log("Oohra: ostrov postaven, hráč " + _nickname);
            if (!_connection.NewCharacter)
            {
                _session.Say("Jsi na ostrově. Nejdřív si najdi vodu.", false);
                return;
            }

            // Nový hráč: nejdřív sen bez slov (návrh 4.2), pak se probudí na ostrově.
            if (DreamView.Disabled)
            {
                WakeUp();
                return;
            }

            gameObject.AddComponent<DreamView>().Play(_session, WakeUp);
        }

        /// <summary>Konec snu: server přestane postavu chránit a hra začne.</summary>
        private void WakeUp()
        {
            _connection.DreamOver();
            _session.Say("Probudil ses. Nejdřív si najdi vodu. Světlejší modrá jsou jezírka, moře je slané.", false);
        }

        private void BuildScene()
        {
            var island = IslandView.Build(_connection.World, _connection);
            _players = new GameObject("Hráči").AddComponent<PlayerViews>();
            _players.Init(_connection);

            var camera = Camera.main;
            island.GetComponentInChildren<SeeThrough>().Follow(_players, camera);
            gameObject.AddComponent<PlayerController>().Init(_session, camera);
            gameObject.AddComponent<DayNightLight>().Init(_session.Clock, FindSun());
            gameObject.AddComponent<BuildMode>().Init(_session, camera);
            new GameObject("Kruh výběru").AddComponent<SelectionRing>().Init(_session);
            gameObject.AddComponent<GameHud>().Init(_session, _players, camera);
            var strangers = new GameObject("Cizinci").AddComponent<StrangerViews>();
            strangers.Init(_connection);
            var speech = gameObject.AddComponent<SpeechHud>();
            speech.Init(_session, _players, camera);
            speech.ShowStrangers(strangers);
            gameObject.AddComponent<SignHud>().Init(_session);
            gameObject.AddComponent<TribeHud>().Init(_session);
            gameObject.AddComponent<CorpseHud>().Init(_session);
            gameObject.AddComponent<SignatureHud>().Init(_session);
            gameObject.AddComponent<ReportHud>().Init(_session);
            gameObject.AddComponent<Hints>().Init(_session, _nickname);
            new GameObject("Těla").AddComponent<CorpseViews>().Init(_connection);
            new GameObject("Značky").AddComponent<SignViews>().Init(_connection);
        }

        private void Disconnect()
        {
            if (_connection != null)
            {
                _connection.Dispose();
            }

            _connection = null;
            _session = null;
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        /// <summary>Přihlašovací okno, dokud hra neběží. Po přerušení spojení ukáže hlášku.</summary>
        private void OnGUI()
        {
            CreateStyles();
            if (_worldBuilt)
            {
                if (_connection != null && _connection.Disconnected)
                {
                    GUI.Box(new Rect(Screen.width / 2f - 200, 100, 400, 40), _status, _title);
                }

                return;
            }

            var rect = new Rect(Screen.width / 2f - 210, Screen.height / 2f - 150, 420, 300);
            GUI.Box(rect, GUIContent.none);
            GUI.Box(rect, GUIContent.none);
            var x = rect.x + 20;
            var y = rect.y + 16;
            GUI.Label(new Rect(x, y, 380, 30), "OOHRA", _title);
            y += 40;
            _host = Field(x, ref y, "Server", _host);
            var port = Field(x, ref y, "Port", _port.ToString());
            int.TryParse(port, out _port);
            _inviteCode = Field(x, ref y, "Kód pozvánky", _inviteCode);
            _nickname = Field(x, ref y, "Přezdívka", _nickname);

            var connecting = _session != null;
            GUI.enabled = !connecting && _nickname.Trim().Length >= 2;
            if (GUI.Button(new Rect(x, y + 6, 160, 34), connecting ? "Připojuji…" : "Připojit"))
            {
                Connect(_host.Trim(), _port, _inviteCode.Trim(), _nickname.Trim());
            }

            GUI.enabled = true;
            GUI.Label(new Rect(x, y + 46, 380, 44), _status, _label);
        }

        private string Field(float x, ref float y, string label, string value)
        {
            GUI.Label(new Rect(x, y, 120, 24), label, _label);
            var result = GUI.TextField(new Rect(x + 120, y, 250, 24), value ?? string.Empty, 32);
            y += 32;
            return result;
        }

        private void CreateStyles()
        {
            if (_label != null)
            {
                return;
            }

            _label = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            _label.normal.textColor = new Color(0.94f, 0.9f, 0.82f);
            _title = new GUIStyle(_label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = new Color(0.86f, 0.64f, 0.25f);
        }

        private static Light FindSun()
        {
            if (RenderSettings.sun != null)
            {
                return RenderSettings.sun;
            }

            foreach (var light in FindObjectsByType<Light>())
            {
                if (light.type == LightType.Directional)
                {
                    return light;
                }
            }

            return null;
        }
    }
}
