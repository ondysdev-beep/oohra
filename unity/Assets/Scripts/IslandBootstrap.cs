using Oohra.Core.Data;
using Oohra.Core.World;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Spouští šedou krabici: vygeneruje ostrov z Oohra.Core, postaví scénu, narodí postavu
    /// a každý snímek posune herní čas a tělo. Pravidla jsou v Core, Unity jen zobrazuje a čte vstup.
    /// </summary>
    public class IslandBootstrap : MonoBehaviour
    {
        [Tooltip("Seed světa. Stejný seed dá stejný ostrov jako TestClient.")]
        [SerializeField] private int _seed = 1;

        [Tooltip("0 = velikost z nastavení (settings.json).")]
        [SerializeField] private int _size = 0;

        [Tooltip("0 = Pradávný, 1 a víc = Nový.")]
        [SerializeField] private int _generation = 0;

        private GameState _state;

        /// <summary>Stav hry (pro testy a ladění).</summary>
        public GameState State => _state;

        private void Awake()
        {
            var size = _size > 0 ? _size : GameData.Settings.WorldWidth;
            var island = IslandGenerator.Build(_seed, size, size);
            _state = new GameState(island, _seed, _generation);
            IslandView.Build(_state);

            var camera = Camera.main;
            var player = CreatePlayer();
            var controller = player.AddComponent<PlayerController>();
            _state.Player = controller;
            controller.Init(_state, camera);
            camera.gameObject.AddComponent<CameraRig>().Init(player.transform);

            gameObject.AddComponent<DayNightLight>().Init(_state.Clock, FindSun());
            gameObject.AddComponent<GameHud>().Init(_state);
            _state.Say("Jsi první člověk na tomhle ostrově. Nejdřív si najdi vodu.", false);
        }

        private void Update()
        {
            _state.Tick(Time.deltaTime);
        }

        private GameObject CreatePlayer()
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Postava";
            player.transform.localScale = new Vector3(0.55f, 0.6f, 0.55f);
            Destroy(player.GetComponent<Collider>());
            Palette.SetColor(player, _generation == 0 ? Palette.Hex("#8a6a45") : Palette.Hex("#c5a97e"));

            // Nos ukazuje, kam se postava dívá.
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Směr";
            nose.transform.SetParent(player.transform);
            nose.transform.localPosition = new Vector3(0f, 0.5f, 0.5f);
            nose.transform.localScale = new Vector3(0.3f, 0.15f, 0.4f);
            Destroy(nose.GetComponent<Collider>());
            Palette.SetColor(nose, Palette.Hex("#dba23f"));
            return player;
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
