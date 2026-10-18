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
    /// Každý strom je jeden objekt podle čísla dlaždice, aby šel později pokácet.
    /// </summary>
    public class TreeViews : MonoBehaviour
    {
        // Kmen v tvaru z kódu má tloušťku kolize na serveru (treeTrunkRadius 0,12 v movement.json).
        private const float TrunkWidth = 0.24f;

        private readonly Dictionary<int, GameObject> _trees = new Dictionary<int, GameObject>();
        private WorldView _world;
        private SeeThrough _crowns;

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

        public void Init(WorldView world, SeeThrough crowns)
        {
            _world = world;
            _crowns = crowns;
            foreach (var tree in world.Trees)
            {
                _trees[tree.TileIndex] = Build(tree);
            }
        }

        private GameObject Build(TreeView tree)
        {
            var x = tree.TileIndex % _world.Width + 0.5;
            var y = tree.TileIndex / _world.Width + 0.5;
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

        /// <summary>Strom jako cíl kliknutí: dlaždice stromu (nasbírat klacky, později kácet).</summary>
        private InteractionTarget Target(TreeView tree)
        {
            return InteractionTarget.ForTile(TargetKind.Tree, tree.TileIndex % _world.Width, tree.TileIndex / _world.Width);
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
