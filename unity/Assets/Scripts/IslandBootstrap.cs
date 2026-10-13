using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Rng;
using Oohra.Core.Time;
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

        private GameClock _clock;
        private Character _character;

        private void Awake()
        {
            var size = _size > 0 ? _size : GameData.Settings.WorldWidth;
            var island = IslandGenerator.Build(_seed, size, size);
            IslandView.Build(island);

            _clock = GameClock.FromSettings();
            _character = new Character(_generation, new Mulberry32(_seed ^ 0x0B0D ^ _generation));
            var spawn = SpawnPoint.PickWild(island, new Mulberry32(_seed ^ 0x5BA7), firstLife: true);

            var camera = Camera.main;
            var player = CreatePlayer();
            player.GetComponent<PlayerController>().Init(island, _character, spawn, camera);
            camera.gameObject.AddComponent<CameraRig>().Init(player.transform);

            gameObject.AddComponent<DayNightLight>().Init(_clock, FindSun());
            gameObject.AddComponent<DebugHud>().Init(_clock, _character);
        }

        private void Update()
        {
            var minutes = _clock.Advance(Time.deltaTime);
            Physiology.Update(_character, _clock, minutes);
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

            player.AddComponent<PlayerController>();
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
