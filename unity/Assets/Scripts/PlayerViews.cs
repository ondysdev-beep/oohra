using System.Collections.Generic;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Všichni hráči ze snímků serveru: vlastní model z katalogu vzhledu (s animacemi), jinak kapsle.
    /// Server posílá polohy asi 15× za sekundu, mezi nimi se postavy plynule posouvají, aby pohyb nebyl trhaný.
    /// </summary>
    public class PlayerViews : MonoBehaviour
    {
        private const float Smoothing = 15f;

        // 1 dlaždice = 1 metr. Postava je vysoká 1,8 m a široká jako kolize na serveru (radius 0,28 v movement.json).
        public const float BodyHeight = 1.8f;
        private const float BodyRadius = 0.28f;

        // Parametry Animatoru vlastního modelu postavy (docs/Grafika.md).
        public const string SpeedParameter = "Rychlost";
        public const string SleepParameter = "Spi";
        public const string DeadParameter = "Mrtvy";

        private readonly Dictionary<int, GameObject> _views = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, int> _shownGeneration = new Dictionary<int, int>();
        private readonly Dictionary<int, Animator> _animators = new Dictionary<int, Animator>();
        private GameConnection _connection;

        /// <summary>Kapsle vlastní postavy (sleduje ji kamera).</summary>
        public Transform Self { get; private set; }

        public void Init(GameConnection connection)
        {
            _connection = connection;
        }

        /// <summary>Poloha hráče na obrazovce (pro jmenovky), nebo null.</summary>
        public Vector3? WorldPositionOf(int playerId)
        {
            GameObject view;
            return _views.TryGetValue(playerId, out view) ? view.transform.position : (Vector3?)null;
        }

        public IEnumerable<int> PlayerIds => _views.Keys;

        private void Update()
        {
            var snapshot = _connection?.Snapshot;
            if (snapshot == null)
            {
                return;
            }

            var seen = new HashSet<int>();
            foreach (var player in snapshot.Players)
            {
                seen.Add(player.Id);
                var view = ViewFor(player.Id);
                Animator animator;
                _animators.TryGetValue(player.Id, out animator);

                // Spánek a smrt ukáže animace, když ji model má (parametry Spi a Mrtvy). Jinak postava leží.
                var dead = player.Dead && !AnimatorTools.Has(animator, DeadParameter, AnimatorControllerParameterType.Bool);
                var asleep = player.Sleeping && !AnimatorTools.Has(animator, SleepParameter, AnimatorControllerParameterType.Bool);
                var target = WorldCoords.ToUnity(player.X, player.Y, dead || asleep ? BodyRadius : BodyHeight / 2);
                var before = view.transform.position;
                view.transform.position = Vector3.Lerp(before, target, 1 - Mathf.Exp(-Smoothing * Time.deltaTime));
                // Úhel na mapě (y dolů) převedený na otočení v Unity (z nahoru).
                var facing = Quaternion.Euler(0f, 90f + player.Facing * Mathf.Rad2Deg, 0f);
                // Mrtvá i spící postava leží (spící ve směru, kam se dívala).
                view.transform.rotation = dead ? Quaternion.Euler(90f, 0f, 0f) : asleep ? facing * Quaternion.Euler(90f, 0f, 0f) : facing;
                if (animator != null)
                {
                    Animate(animator, player, before, view.transform.position);
                }
            }

            RemoveMissing(seen);
        }

        private GameObject ViewFor(int id)
        {
            GameObject view;
            var generation = GenerationOf(id);
            if (_views.TryGetValue(id, out view))
            {
                int shown;
                if (_shownGeneration.TryGetValue(id, out shown) && shown != generation)
                {
                    Paint(view, generation);
                    _shownGeneration[id] = generation;
                }

                return view;
            }

            view = VisualCatalog.Current.Player != null ? CreateModel() : CreateCapsule();
            view.name = id == _connection.PlayerId ? "Postava" : "Hráč " + id;
            var animator = view.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                _animators[id] = animator;
            }

            Paint(view, generation);
            _shownGeneration[id] = generation;
            _views[id] = view;
            if (id == _connection.PlayerId)
            {
                Self = view.transform;
            }

            return view;
        }

        /// <summary>Kapsle s „nosem“, který ukazuje, kam se postava dívá. Střed kapsle je ve výšce půl postavy.</summary>
        private GameObject CreateCapsule()
        {
            var view = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            view.transform.SetParent(transform);
            view.transform.localScale = new Vector3(BodyRadius * 2, BodyHeight / 2, BodyRadius * 2);
            Destroy(view.GetComponent<Collider>());

            var nose = IslandView.Primitive(PrimitiveType.Cube, "Směr", view.transform, Palette.Hex("#dba23f"));
            nose.transform.localPosition = new Vector3(0f, 0.5f, 0.5f);
            nose.transform.localScale = new Vector3(0.3f, 0.15f, 0.4f);
            return view;
        }

        /// <summary>
        /// Vlastní model z katalogu vzhledu. Kořen je jako u kapsle ve výšce půl postavy (podle něj míří kamera
        /// a jmenovky), model má nohy o půl postavy níž, tedy na zemi.
        /// </summary>
        private GameObject CreateModel()
        {
            var view = new GameObject();
            view.transform.SetParent(transform);
            var model = VisualCatalog.Spawn(VisualCatalog.Current.Player, view.transform, "Model");
            model.transform.localPosition = new Vector3(0f, -BodyHeight / 2, 0f);
            return view;
        }

        /// <summary>Do animace jde rychlost (m/s), spánek a smrt. Jen do parametrů, které model má.</summary>
        private static void Animate(Animator animator, PlayerSnapshot player, Vector3 before, Vector3 after)
        {
            var moved = new Vector2(after.x - before.x, after.z - before.z).magnitude;
            var speed = Time.deltaTime > 0 ? moved / Time.deltaTime : 0f;
            AnimatorTools.SetFloat(animator, SpeedParameter, player.Dead || player.Sleeping ? 0f : speed);
            AnimatorTools.SetBool(animator, SleepParameter, player.Sleeping);
            AnimatorTools.SetBool(animator, DeadParameter, player.Dead);
        }

        /// <summary>Pradávní jsou tmavší (kožešina), Noví světlejší. U vlastního modelu jen části se SpeciesTint.</summary>
        private static void Paint(GameObject view, int generation)
        {
            var color = generation == 0 ? Palette.Hex("#8a6a45") : Palette.Hex("#c5a97e");
            if (view.GetComponent<Renderer>() != null)
            {
                Palette.SetColor(view, color);
            }
            else
            {
                SpeciesTint.ApplyMarked(view, color);
            }
        }

        private int GenerationOf(int id)
        {
            PlayerInfo info;
            return _connection.Players.TryGetValue(id, out info) ? info.Generation : 0;
        }

        private void RemoveMissing(HashSet<int> seen)
        {
            var gone = new List<int>();
            foreach (var id in _views.Keys)
            {
                if (!seen.Contains(id))
                {
                    gone.Add(id);
                }
            }

            foreach (var id in gone)
            {
                Destroy(_views[id]);
                _views.Remove(id);
                _shownGeneration.Remove(id);
                _animators.Remove(id);
            }
        }
    }
}
