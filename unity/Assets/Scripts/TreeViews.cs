using System.Collections.Generic;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Stromy ostrova podle druhu a výšky ze serveru (návrh 0.8, kap. 2). Vlastní model z katalogu vzhledu
    /// (políčko druhu, jinak společný seznam Trees), jinak tvar z kódu podle druhu: dub s kulatou korunou,
    /// štíhlý vysoký jehličnan, bříza se světlým kmenem, nízká široká vrba, suchý strom bez listí.
    /// Každý strom je jeden objekt podle čísla dlaždice. Pokácený strom (stav ze serveru) chvíli praská a kymácí se,
    /// pak spadne na stranu od sekáče a zbude pařez (model Stump z katalogu, jinak nízký válec). Ležící kmen kreslí TimberViews.
    /// </summary>
    public class TreeViews : MonoBehaviour
    {
        // Kmen v tvaru z kódu má tloušťku kolize na serveru (treeTrunkRadius 0,3 v movement.json).
        private const float TrunkWidth = 0.6f;

        // Když server časy pádu nepošle (starší zpráva), praská a padá tolik sekund (jako woodcutting.json).
        private const float DefaultCrack = 2f;
        private const float DefaultFall = 3.5f;

        private readonly Dictionary<int, GameObject> _trees = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Felling> _falling = new Dictionary<int, Felling>();
        private readonly Dictionary<int, GameObject> _stumps = new Dictionary<int, GameObject>();

        // Stromy, které nejsou ze seedu (mladé a dorostlé): dlaždice → druh a jak vysoký je teď nakreslený.
        private readonly Dictionary<int, TreeStateView> _young = new Dictionary<int, TreeStateView>();
        private readonly Dictionary<int, TreeKind> _kinds = new Dictionary<int, TreeKind>();

        // Kde na mapě stojí strom ze seedu (už ne uprostřed dlaždice). Pařez po něm je na stejném místě.
        private readonly Dictionary<int, Vector2> _positions = new Dictionary<int, Vector2>();
        private WorldView _world;
        private SeeThrough _crowns;
        private GameConnection _connection;
        private TreeStateView[] _lastStates;

        private sealed class Felling
        {
            public float Started;
            public float Crack;
            public float Fall;
            public Vector3 Direction;
            public Quaternion Upright;
            public float Height;
        }

        /// <summary>Kolik mladých a dorostlých stromů (ne ze seedu) je ve scéně, pro testy.</summary>
        public int YoungCount
        {
            get { return _young.Count; }
        }

        /// <summary>Pařez na dlaždici (pro testy), nebo null.</summary>
        public GameObject StumpAt(int tileIndex)
        {
            GameObject stump;
            return _stumps.TryGetValue(tileIndex, out stump) ? stump : null;
        }

        /// <summary>Strom na dlaždici (pro testy a kácení), nebo null.</summary>
        public GameObject TreeAt(int tileIndex)
        {
            GameObject tree;
            return _trees.TryGetValue(tileIndex, out tree) ? tree : null;
        }

        public int Count
        {
            get { return _trees.Count; }
        }

        public void Init(WorldView world, SeeThrough crowns, GameConnection connection = null)
        {
            _world = world;
            _crowns = crowns;
            _connection = connection;
            foreach (var tree in world.Trees)
            {
                if (tree.X > 0f || tree.Y > 0f) _positions[tree.TileIndex] = new Vector2(tree.X, tree.Y);
                _trees[tree.TileIndex] = Build(tree);
                _kinds[tree.TileIndex] = tree.Kind;
            }
        }

        private void Update()
        {
            if (_connection != null && _connection.TreeStates != _lastStates)
            {
                _lastStates = _connection.TreeStates;
                var young = new HashSet<int>();
                foreach (var state in _lastStates)
                {
                    switch (state.State)
                    {
                        case TreeState.Falling:
                            StartFalling(state);
                            break;
                        case TreeState.Stump:
                            Stump(state.TileIndex);
                            break;
                        default:
                            young.Add(state.TileIndex);
                            ShowYoung(state);
                            break;
                    }
                }

                RemoveWithered(young);
            }

            foreach (var pair in _falling)
            {
                Animate(_trees[pair.Key].transform, pair.Value);
            }
        }

        /// <summary>
        /// Mladý strom roste po stupních (server pošle každý stupeň), nakreslí se tak vysoký, jak je teď.
        /// Dorostlý (Grown) je normální strom v plné výšce: jde na něj kliknout a kácet ho.
        /// </summary>
        private void ShowYoung(TreeStateView state)
        {
            TreeStateView shown;
            if (_young.TryGetValue(state.TileIndex, out shown) && shown.Equals(state))
            {
                return;
            }

            if (_falling.ContainsKey(state.TileIndex) || _stumps.ContainsKey(state.TileIndex))
            {
                return;
            }

            GameObject old;
            if (_trees.TryGetValue(state.TileIndex, out old))
            {
                Destroy(old);
            }

            var growth = state.State == TreeState.Grown ? 1f : state.Growth;
            var height = Mathf.Max(0.4f, state.Height * growth);
            var variant = (state.TileIndex * 37 % 100) / 100f;
            var view = new TreeView(state.TileIndex, variant, state.Kind, height);
            var sapling = state.State == TreeState.Sapling ? VisualCatalog.Current.Sapling(variant) : null;
            _trees[state.TileIndex] = sapling != null ? Sapling(sapling, view) : Build(view);
            _kinds[state.TileIndex] = state.Kind;
            _young[state.TileIndex] = state;
        }

        /// <summary>Mladý strom, který server už neposílá (uschl u stavby), zmizí.</summary>
        private void RemoveWithered(HashSet<int> young)
        {
            foreach (var tile in new List<int>(_young.Keys))
            {
                if (young.Contains(tile) || _stumps.ContainsKey(tile) || _falling.ContainsKey(tile))
                {
                    continue;
                }

                GameObject tree;
                if (_trees.TryGetValue(tile, out tree))
                {
                    Destroy(tree);
                    _trees.Remove(tile);
                }

                _crowns.Remove(tile);
                _young.Remove(tile);
            }
        }

        /// <summary>
        /// Strom začal padat (server): zapraská a začne se kymácet. Časy jsou ze serveru (kolik už padá, jak dlouho
        /// praská a padá), takže hráč, který přišel uprostřed pádu, vidí totéž.
        /// </summary>
        private void StartFalling(TreeStateView state)
        {
            GameObject tree;
            if (_falling.ContainsKey(state.TileIndex) || !_trees.TryGetValue(state.TileIndex, out tree) || !tree.activeSelf)
            {
                return;
            }

            var angle = state.Angle;
            _falling[state.TileIndex] = new Felling
            {
                Started = Time.time - state.Elapsed,
                Crack = state.Crack > 0f ? state.Crack : DefaultCrack,
                Fall = state.Fall > 0f ? state.Fall : DefaultFall,
                Direction = new Vector3(Mathf.Cos(angle), 0f, -Mathf.Sin(angle)),
                Upright = tree.transform.rotation,
                Height = Mathf.Max(2f, VisualCatalog.BoundsOf(tree).size.y),
            };
            Ambience.PlayAt(Ambience.Creak(), tree.transform.position + Vector3.up * 2f, 0.8f);
        }

        /// <summary>
        /// Praskání (strom se kymácí čím dál víc), pak pád: nakloní se ke směru pádu pomalu a pak čím dál rychleji,
        /// jako by ho táhla tíha (úhel roste s druhou mocninou času).
        /// </summary>
        private static void Animate(Transform tree, Felling felling)
        {
            tree.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, felling.Direction), Tilt(Time.time - felling.Started, felling.Crack, felling.Fall)) * felling.Upright;
        }

        /// <summary>
        /// Jak moc je strom nakloněný (0 stojí, 1 leží) v čase t od posledního seknutí: během praskání se kymácí
        /// o pár stupňů (víc a víc), pak padá s druhou mocninou času. Těsně před dopadem je skoro vodorovně.
        /// </summary>
        public static float Tilt(float t, float crack, float fall)
        {
            if (t < crack)
            {
                var grow = t / crack;
                return Mathf.Sin(t * 9f) * 0.02f * grow + 0.03f * grow * grow;
            }

            var u = Mathf.Clamp01((t - crack) / fall);
            return Mathf.Lerp(0.03f, 0.97f, u * u);
        }

        /// <summary>Ze stromu zbyl pařez: strom zmizí (kmen na zemi kreslí TimberViews), na jeho místě je pařez.</summary>
        private void Stump(int tileIndex)
        {
            if (_stumps.ContainsKey(tileIndex))
            {
                return;
            }

            GameObject tree;
            if (_trees.TryGetValue(tileIndex, out tree))
            {
                Felling felling;
                if (_falling.TryGetValue(tileIndex, out felling))
                {
                    Ambience.PlayTreeFall(tree.transform.position);
                    ParticleLook.DustBurst(tree.transform.position, felling.Direction, felling.Height);
                }

                tree.SetActive(false);
            }

            _falling.Remove(tileIndex);
            _crowns.Remove(tileIndex);
            var (x, y) = PositionOf(tileIndex);
            TreeKind kind;
            if (!_kinds.TryGetValue(tileIndex, out kind)) kind = TreeKind.Oak;
            _young.Remove(tileIndex);
            _stumps[tileIndex] = CreateStump(transform, kind, x, y);
        }

        /// <summary>Pařez: model z katalogu (Stump, srovná se na výšku 0,6 m), jinak nízký válec v barvě kůry.</summary>
        public static GameObject CreateStump(Transform parent, TreeKind kind, double x, double y)
        {
            const float height = 0.6f;
            var prefab = VisualCatalog.Current.Stump(GroundView.Hash01((int)x, (int)y, 41));
            GameObject stump;
            if (prefab != null)
            {
                stump = VisualCatalog.Spawn(prefab, parent, "Pařez", height: height);
                stump.transform.position = WorldCoords.ToUnity(x, y);
            }
            else
            {
                stump = IslandView.Primitive(PrimitiveType.Cylinder, "Pařez", parent, TimberViews.BarkColor(kind));
                stump.transform.position = WorldCoords.ToUnity(x, y, height / 2);
                stump.transform.localScale = new Vector3(TrunkWidth * 1.4f, height / 2, TrunkWidth * 1.4f);
            }

            return stump;
        }

        /// <summary>Mladý stromek z katalogu (Saplings), srovnaný na výšku podle růstu. Kliknout na něj jde (nabídka se zavře).</summary>
        private GameObject Sapling(GameObject prefab, TreeView tree)
        {
            var (x, y) = PositionOf(tree.TileIndex);
            var model = VisualCatalog.Spawn(prefab, transform, "Mladý strom " + tree.TileIndex, height: tree.Height);
            model.transform.position = WorldCoords.ToUnity(x, y);
            model.transform.rotation = Quaternion.Euler(0f, tree.TileIndex * 137 % 360, 0f);
            return model;
        }

        /// <summary>Kde stojí strom na dlaždici: strom ze seedu na svém místě, mladý a dorostlý uprostřed dlaždice.</summary>
        private (double X, double Y) PositionOf(int tileIndex)
        {
            Vector2 position;
            return _positions.TryGetValue(tileIndex, out position)
                ? (position.x, position.y)
                : (tileIndex % _world.Width + 0.5, tileIndex / _world.Width + 0.5);
        }

        /// <summary>
        /// Nejširší koruna vůči výšce stromu podle druhu (dub a vrba široké, jehličnan a bříza úzké). Model z katalogu,
        /// který je širší, se do šířky zúží, ať sousední stromy nevypadají každý jinak velký.
        /// </summary>
        public static float MaxCrownRatio(TreeKind kind)
        {
            switch (kind)
            {
                case TreeKind.Oak: return 0.7f;
                case TreeKind.Willow: return 0.75f;
                case TreeKind.Conifer: return 0.38f;
                case TreeKind.Birch: return 0.42f;
                default: return 0.55f;
            }
        }

        private GameObject Build(TreeView tree)
        {
            var (x, y) = PositionOf(tree.TileIndex);
            var prefab = VisualCatalog.Current.Tree(tree.Kind, tree.Variant);
            var root = prefab != null ? Model(prefab, tree, x, y) : Shape(tree, x, y);
            root.name = "Strom " + tree.TileIndex + " (" + Texts.TreeName(tree.Kind) + ")";
            return root;
        }

        /// <summary>
        /// Model z katalogu srovnaný na výšku stromu, natočený podle dlaždice (ať nejsou všechny stejně).
        /// Zprůhlední celý, když zakrývá postavu (koruna začíná zhruba ve 40 % výšky).
        /// </summary>
        private GameObject Model(GameObject prefab, TreeView tree, double x, double y)
        {
            var model = VisualCatalog.Spawn(prefab, transform, "Strom", height: tree.Height);
            model.transform.position = WorldCoords.ToUnity(x, y);
            model.transform.rotation = Quaternion.Euler(0f, tree.TileIndex * 137 % 360, 0f);
            var bounds = VisualCatalog.BoundsOf(model);
            var width = Mathf.Max(bounds.size.x, bounds.size.z);
            var widest = MaxCrownRatio(tree.Kind) * bounds.size.y;
            if (width > widest && width > 0.01f)
            {
                // Moc široký model se do šířky zúží (výška zůstane).
                var squeeze = widest / width;
                var scale = model.transform.localScale;
                model.transform.localScale = new Vector3(scale.x * squeeze, scale.y, scale.z * squeeze);
                bounds = VisualCatalog.BoundsOf(model);
            }

            var radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            _crowns.Add(tree.TileIndex, model, null, x, y, radius, bounds.max.y * 0.4f, bounds.max.y);
            Pickable.Attach(model, Target(tree));
            return model;
        }

        /// <summary>Tvar z kódu podle druhu: kmen (válec) a koruna (koule nebo kapsle), suchý strom jen holé větve.</summary>
        private GameObject Shape(TreeView tree, double x, double y)
        {
            var h = tree.Height;
            var root = new GameObject("Strom");
            root.transform.SetParent(transform);
            root.transform.position = WorldCoords.ToUnity(x, y);
            root.transform.rotation = Quaternion.Euler(0f, tree.TileIndex * 137 % 360, 0f);

            var look = LookOf(tree.Kind, tree.Variant);
            var trunk = IslandView.Primitive(PrimitiveType.Cylinder, "Kmen", root.transform, look.Trunk);
            trunk.transform.localPosition = new Vector3(0f, look.TrunkShare * h / 2, 0f);
            trunk.transform.localScale = new Vector3(TrunkWidth * look.TrunkWidth, look.TrunkShare * h / 2, TrunkWidth * look.TrunkWidth);
            Pickable.Attach(trunk, Target(tree));

            if (tree.Kind == TreeKind.Dead)
            {
                Branch(root.transform, look.Trunk, h * 0.6f, 40f, h * 0.25f);
                Branch(root.transform, look.Trunk, h * 0.75f, -35f, h * 0.2f);
                return root;
            }

            var crown = IslandView.Primitive(look.Crown, "Koruna", root.transform, look.Leaves);
            var size = new Vector3(look.CrownWidth * h, look.CrownHeight * h, look.CrownWidth * h);
            // Kapsle je v Unity dvakrát vyšší, než je její měřítko na výšku.
            var scale = look.Crown == PrimitiveType.Capsule ? new Vector3(size.x, size.y / 2, size.z) : size;
            var center = look.CrownCenter * h;
            crown.transform.localPosition = new Vector3(0f, center, 0f);
            crown.transform.localScale = scale;
            _crowns.Add(tree.TileIndex, crown, look.Leaves, x, y, size.x / 2, center - size.y / 2, center + size.y / 2);
            Pickable.Attach(crown, Target(tree));
            return root;
        }

        private static void Branch(Transform parent, Color color, float height, float angle, float length)
        {
            var branch = IslandView.Primitive(PrimitiveType.Cylinder, "Větev", parent, color);
            branch.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            branch.transform.localScale = new Vector3(0.08f, length / 2, 0.08f);
            branch.transform.localPosition = new Vector3(0f, height, 0f) + branch.transform.up * (length / 2);
        }

        /// <summary>Strom jako cíl kliknutí: dlaždice stromu (nasbírat klacky, kácet), postava jde ke kmeni.</summary>
        private InteractionTarget Target(TreeView tree)
        {
            var (x, y) = PositionOf(tree.TileIndex);
            return InteractionTarget.ForTree(tree.TileIndex % _world.Width, tree.TileIndex / _world.Width, x, y);
        }

        /// <summary>Jak vypadá tvar z kódu: barvy, podíl výšky kmene, tvar, velikost a výška koruny (podíly výšky stromu).</summary>
        private struct Look
        {
            public Color Trunk;
            public Color Leaves;
            public float TrunkShare;
            public float TrunkWidth;
            public PrimitiveType Crown;
            public float CrownWidth;
            public float CrownHeight;
            public float CrownCenter;
        }

        private static Look LookOf(TreeKind kind, float variant)
        {
            var bark = Palette.Hex("#5a4331");
            switch (kind)
            {
                case TreeKind.Conifer:
                    return new Look { Trunk = bark, Leaves = Palette.Hex("#2f5232"), TrunkShare = 0.35f, TrunkWidth = 1f, Crown = PrimitiveType.Capsule, CrownWidth = 0.3f, CrownHeight = 0.8f, CrownCenter = 0.6f };
                case TreeKind.Birch:
                    return new Look { Trunk = Palette.Hex("#d6d0c0"), Leaves = Palette.Hex("#6f9446"), TrunkShare = 0.6f, TrunkWidth = 0.8f, Crown = PrimitiveType.Sphere, CrownWidth = 0.38f, CrownHeight = 0.48f, CrownCenter = 0.72f };
                case TreeKind.Willow:
                    return new Look { Trunk = bark, Leaves = Palette.Hex("#6b7d4a"), TrunkShare = 0.45f, TrunkWidth = 1.3f, Crown = PrimitiveType.Sphere, CrownWidth = 0.95f, CrownHeight = 0.6f, CrownCenter = 0.6f };
                case TreeKind.Dead:
                    return new Look { Trunk = Palette.Hex("#6e6457"), TrunkShare = 1f, TrunkWidth = 0.85f };
                default:
                    return new Look { Trunk = bark, Leaves = variant > 0.5f ? Palette.Hex("#4e7d3a") : Palette.Hex("#3d6b30"), TrunkShare = 0.5f, TrunkWidth = 1.2f, Crown = PrimitiveType.Sphere, CrownWidth = 0.55f, CrownHeight = 0.5f, CrownCenter = 0.7f };
            }
        }
    }
}
