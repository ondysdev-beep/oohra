using System.Collections.Generic;
using Oohra.Core.Items;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Všichni hráči ze snímků serveru: postava podle generace (GenerationLook), vlastní model z katalogu vzhledu
    /// (s animacemi), jinak kapsle s doplňky.
    /// Server posílá polohy asi 15× za sekundu, mezi nimi se postavy plynule posouvají, aby pohyb nebyl trhaný.
    /// </summary>
    public class PlayerViews : MonoBehaviour
    {
        /// <summary>Skok delší než tohle (metry) se nevyhlazuje (vchod do jeskyně).</summary>
        public const float TeleportDistance = 20f;

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
        private readonly Dictionary<int, HandTorch> _torches = new Dictionary<int, HandTorch>();
        private readonly Dictionary<int, GameObject> _carried = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> _bags = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, ItemKind?> _bagKinds = new Dictionary<int, ItemKind?>();
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
                // V patře stojí postava na podlaze patra (o podlaží výš).
                var target = WorldCoords.ToUnity(player.X, player.Y, player.Level * StructureViews.LevelHeight + (dead || asleep ? BodyRadius : BodyHeight / 2));
                var before = view.transform.position;
                // Průchod vchodem do jeskyně je skok daleko: postava se přenese hned, neklouže přes mapu.
                view.transform.position = (target - before).sqrMagnitude > TeleportDistance * TeleportDistance
                    ? target
                    : Vector3.Lerp(before, target, 1 - Mathf.Exp(-Smoothing * Time.deltaTime));
                // Úhel na mapě (y dolů) převedený na otočení v Unity (z nahoru).
                var facing = Quaternion.Euler(0f, 90f + player.Facing * Mathf.Rad2Deg, 0f);
                // Mrtvá i spící postava leží (spící ve směru, kam se dívala).
                view.transform.rotation = dead ? Quaternion.Euler(90f, 0f, 0f) : asleep ? facing * Quaternion.Euler(90f, 0f, 0f) : facing;
                if (animator != null)
                {
                    Animate(animator, player, before, view.transform.position);
                }

                ShowTorch(player.Id, view, player.Torch && !player.Dead && !player.Sleeping);
                ShowCarried(player.Id, view, player.Carry && !player.Dead && !player.Sleeping);
                ShowBag(player.Id, view, player.Dead ? null : player.Bag);
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
                    // Nové narození (Pradávný → Nový, nebo vyšší generace): jiná postava.
                    Dress(id, view, generation);
                }

                return view;
            }

            view = new GameObject(id == _connection.PlayerId ? "Postava" : "Hráč " + id);
            view.transform.SetParent(transform);
            Dress(id, view, generation);
            _views[id] = view;
            if (id == _connection.PlayerId)
            {
                Self = view.transform;
            }
            else
            {
                // Na jiné hráče jde kliknout (pozvat do kmene). Na sebe ne, to by zakrylo zem pod nohama.
                Pickable.Attach(view, InteractionTarget.ForPlayer(id, 0, 0));
            }

            return view;
        }

        /// <summary>
        /// Oblékne postavu podle generace (GenerationLook): vlastní model z katalogu, jinak kapsle s doplňky.
        /// Kořen je ve výšce půl postavy (podle něj míří kamera a jmenovky), postava má nohy o půl postavy níž, tedy na zemi.
        /// </summary>
        private void Dress(int id, GameObject view, int generation)
        {
            var old = view.transform.Find(GenerationLook.FigureName);
            if (old != null)
            {
                old.name = "Stará figura";
                Destroy(old.gameObject);
            }

            var figure = GenerationLook.Figure(view.transform, generation);
            figure.transform.localPosition = new Vector3(0f, -BodyHeight / 2, 0f);
            _shownGeneration[id] = generation;
            var animator = figure.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                _animators[id] = animator;
            }
            else
            {
                _animators.Remove(id);
                _torches.Remove(id);
                _carried.Remove(id);
                _bags.Remove(id);
                _bagKinds.Remove(id);
            }
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

        /// <summary>Pochodeň v ruce: přidá se, až ji postava poprvé zapálí. Spící a mrtvá postava ji nedrží.</summary>
        private void ShowTorch(int id, GameObject view, bool lit)
        {
            HandTorch torch;
            if (!_torches.TryGetValue(id, out torch))
            {
                if (!lit)
                {
                    return;
                }

                torch = HandTorch.Attach(view.transform);
                _torches[id] = torch;
            }

            if (torch.gameObject.activeSelf != lit)
            {
                torch.gameObject.SetActive(lit);
            }
        }

        /// <summary>Klad na rameni (model LogPiece z katalogu, jinak válec), když hráč nese klad.</summary>
        private void ShowCarried(int id, GameObject view, bool carrying)
        {
            GameObject piece;
            _carried.TryGetValue(id, out piece);
            if (carrying == (piece != null && piece.activeSelf))
            {
                return;
            }

            if (piece == null)
            {
                var timber = new TimberView(0, TimberKind.Piece, TreeKind.Oak, 0f, 0f, 0f, 1);
                piece = TimberViews.Create(view.transform, timber);
                // Na kliknutí není: je to jen ukázka toho, co hráč nese.
                foreach (var collider in piece.GetComponentsInChildren<Collider>()) Destroy(collider);
                Destroy(piece.GetComponent<Pickable>());
                piece.name = "Nesený klad";
                piece.transform.localPosition = new Vector3(0.15f, BodyHeight / 2 - 0.2f, 0f);
                piece.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                _carried[id] = piece;
            }

            piece.SetActive(carrying);
        }

        /// <summary>Batoh na zádech: model z katalogu (SmallBag, LargeBag), jinak kvádr v barvě batohu.</summary>
        private void ShowBag(int id, GameObject view, ItemKind? bag)
        {
            ItemKind? shown;
            if (_bagKinds.TryGetValue(id, out shown) && shown == bag)
            {
                return;
            }

            GameObject old;
            if (_bags.TryGetValue(id, out old) && old != null)
            {
                Destroy(old);
            }

            _bagKinds[id] = bag;
            _bags.Remove(id);
            if (!bag.HasValue)
            {
                return;
            }

            _bags[id] = CreateBag(view.transform, bag.Value);
        }

        /// <summary>Batoh na zádech postavy (kořen postavy je v půlce výšky, záda jsou na -Z).</summary>
        public static GameObject CreateBag(Transform figure, ItemKind bag)
        {
            var large = bag == ItemKind.LargeBag;
            var height = large ? 0.6f : 0.4f;
            var prefab = large ? VisualCatalog.Current.LargeBag : VisualCatalog.Current.SmallBag;
            GameObject go;
            if (prefab != null)
            {
                go = VisualCatalog.Spawn(prefab, figure, "Batoh", height: height);
                go.transform.localPosition = new Vector3(0f, 0.05f, -0.24f);
                go.transform.localRotation = Quaternion.identity;
            }
            else
            {
                go = IslandView.Primitive(PrimitiveType.Cube, "Batoh", figure, Palette.Material(bag));
                go.transform.localPosition = new Vector3(0f, 0.15f, -0.27f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = new Vector3(large ? 0.42f : 0.34f, height, large ? 0.22f : 0.16f);
            }

            return go;
        }

        /// <summary>Jaký batoh má hráč na zádech (vidět ve scéně), pro testy.</summary>
        public ItemKind? BagOf(int playerId)
        {
            ItemKind? bag;
            return _bagKinds.TryGetValue(playerId, out bag) ? bag : null;
        }

        /// <summary>Nese hráč klad (vidět ve scéně)? Pro testy.</summary>
        public bool IsCarrying(int playerId)
        {
            GameObject piece;
            return _carried.TryGetValue(playerId, out piece) && piece != null && piece.activeSelf;
        }

        /// <summary>Drží hráč zapálenou pochodeň (vidět ve scéně)? Pro testy.</summary>
        public bool HasLitTorch(int playerId)
        {
            HandTorch torch;
            return _torches.TryGetValue(playerId, out torch) && torch.Lit;
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
