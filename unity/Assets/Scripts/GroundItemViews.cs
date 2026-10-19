using System.Collections.Generic;
using Oohra.Core.Items;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Věci na zemi podle serveru (hromádky, které hráči položili): klacky a dřevo leží zkřížené, kameny jako
    /// oblázky, rostliny jako trs listí v barvě druhu, ostatní jako malý balík v barvě věci. Čím víc kusů, tím
    /// víc tvarů (nejvýš pár). Na hromádku jde kliknout a sebrat ji. Tvar je z kódu (katalog vzhledu zatím ne).
    /// </summary>
    public class GroundItemViews : MonoBehaviour
    {
        private readonly Dictionary<int, GameObject> _shown = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GroundItemView> _views = new Dictionary<int, GroundItemView>();
        private GameConnection _connection;
        private GroundItemView[] _last;

        public int Count
        {
            get { return _shown.Count; }
        }

        /// <summary>Hromádka podle čísla (pro testy), nebo null.</summary>
        public GameObject PileObject(int id)
        {
            GameObject go;
            return _shown.TryGetValue(id, out go) ? go : null;
        }

        public void Init(GameConnection connection)
        {
            _connection = connection;
        }

        private void Update()
        {
            if (_connection == null || ReferenceEquals(_connection.GroundItems, _last))
            {
                return;
            }

            _last = _connection.GroundItems;
            var seen = new HashSet<int>();
            foreach (var pile in _last)
            {
                seen.Add(pile.Id);
                GroundItemView before;
                if (_views.TryGetValue(pile.Id, out before) && before.Equals(pile))
                {
                    continue;
                }

                // Nová hromádka nebo změna (přibylo, ubylo, spadla z patra): postaví se znovu.
                Remove(pile.Id);
                _shown[pile.Id] = Create(transform, pile, _connection.World);
                _views[pile.Id] = pile;
            }

            foreach (var id in new List<int>(_shown.Keys))
            {
                if (!seen.Contains(id))
                {
                    Remove(id);
                }
            }
        }

        private void Remove(int id)
        {
            GameObject go;
            if (_shown.TryGetValue(id, out go))
            {
                Destroy(go);
                _shown.Remove(id);
                _views.Remove(id);
            }
        }

        /// <summary>Hromádka na zemi (nebo na podlaze patra).</summary>
        public static GameObject Create(Transform parent, GroundItemView pile, WorldView world)
        {
            var root = new GameObject("Na zemi " + pile.Id + " (" + pile.Stack.Kind + " ×" + pile.Stack.Count + ")");
            root.transform.SetParent(parent);
            root.transform.position = WorldCoords.ToUnity(pile.X, pile.Y, pile.Level * StructureViews.LevelHeight);
            // Každá hromádka je trochu jinak natočená (podle čísla, ať se při přestavění nehýbe).
            root.transform.rotation = Quaternion.Euler(0f, pile.Id * 47 % 360, 0f);

            var kind = pile.Stack.Kind;
            if (IsLong(kind))
            {
                Sticks(root.transform, pile);
            }
            else if (kind == ItemKind.Stone || kind == ItemKind.Flint)
            {
                Pebbles(root.transform, pile);
            }
            else if (kind == ItemKind.Plant && world != null && pile.Stack.SpeciesId >= 0 && pile.Stack.SpeciesId < world.Species.Length)
            {
                Leaves(root.transform, pile, Palette.Leaf(world.Species[pile.Stack.SpeciesId]));
            }
            else
            {
                Bundles(root.transform, pile);
            }

            Pickable.Attach(root, InteractionTarget.ForGroundItem(pile.Id, pile.X, pile.Y));
            return root;
        }

        private static bool IsLong(ItemKind kind)
        {
            return kind == ItemKind.Stick || kind == ItemKind.Spear || kind == ItemKind.Wood || kind == ItemKind.Splinter;
        }

        /// <summary>Kolik tvarů ukázat: jeden za kus, nejvýš max.</summary>
        private static int Shapes(GroundItemView pile, int max)
        {
            return Mathf.Clamp(pile.Stack.Count, 1, max);
        }

        /// <summary>Klacky (oštěp, dřevo, třísky) leží zkřížené přes sebe.</summary>
        private static void Sticks(Transform root, GroundItemView pile)
        {
            var kind = pile.Stack.Kind;
            var length = kind == ItemKind.Spear ? 1.6f : kind == ItemKind.Splinter ? 0.3f : 0.7f;
            var thickness = kind == ItemKind.Wood ? 0.1f : kind == ItemKind.Splinter ? 0.03f : 0.045f;
            var color = Palette.Material(kind);
            var count = Shapes(pile, 5);
            for (var i = 0; i < count; i++)
            {
                var stick = IslandView.Primitive(PrimitiveType.Cylinder, "Kus", root, color);
                // Válec stojí podél osy Y: položí se a každý další se pootočí a trochu zvedne.
                stick.transform.localRotation = Quaternion.Euler(0f, i * 61f, 90f);
                stick.transform.localPosition = new Vector3(0f, thickness / 2 + i * thickness * 0.6f, 0f);
                stick.transform.localScale = new Vector3(thickness, length / 2f, thickness);
            }
        }

        /// <summary>Kameny jako oblázky v kroužku.</summary>
        private static void Pebbles(Transform root, GroundItemView pile)
        {
            var color = Palette.Material(pile.Stack.Kind);
            var count = Shapes(pile, 6);
            for (var i = 0; i < count; i++)
            {
                var stone = IslandView.Primitive(PrimitiveType.Sphere, "Kámen", root, color);
                var angle = i * 2.4f;
                var radius = i == 0 ? 0f : 0.12f;
                stone.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, 0.05f, Mathf.Sin(angle) * radius);
                stone.transform.localScale = new Vector3(0.16f, 0.1f, 0.13f);
            }
        }

        /// <summary>Utržené části rostliny: trs listí v barvě druhu.</summary>
        private static void Leaves(Transform root, GroundItemView pile, Color color)
        {
            var count = Shapes(pile, 4);
            for (var i = 0; i < count; i++)
            {
                var leaf = IslandView.Primitive(PrimitiveType.Sphere, "List", root, color);
                var angle = i * 2.1f;
                leaf.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.08f, 0.03f, Mathf.Sin(angle) * 0.08f);
                leaf.transform.localScale = new Vector3(0.18f, 0.05f, 0.12f);
            }
        }

        /// <summary>Ostatní věci (kůže, kosti, nástroje, batoh, maso…): malé balíky na sobě.</summary>
        private static void Bundles(Transform root, GroundItemView pile)
        {
            var color = Palette.Material(pile.Stack.Kind);
            var count = Shapes(pile, 3);
            for (var i = 0; i < count; i++)
            {
                var bundle = IslandView.Primitive(PrimitiveType.Cube, "Věc", root, color);
                bundle.transform.localRotation = Quaternion.Euler(0f, i * 35f, 0f);
                bundle.transform.localPosition = new Vector3(0f, 0.04f + i * 0.08f, 0f);
                bundle.transform.localScale = new Vector3(0.28f, 0.08f, 0.2f);
            }
        }
    }
}
