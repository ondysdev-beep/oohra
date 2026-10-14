using System.Collections.Generic;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Zvířata v okolí podle zpráv serveru, z jednoduchých tvarů (tělo, hlava, nohy). Poloha se mezi zprávami
    /// plynule dopočítává. Barva ukazuje, co zvíře dělá: zaměřené a ve výpadu zčervená (pozor!), vyčerpané
    /// zesvětlá (teď je zranitelné). Mrtvé leží na boku, spící králík se přikrčí.
    /// </summary>
    public class AnimalViews : MonoBehaviour
    {
        private const float Smoothing = 12f;
        private static readonly Color DangerColor = new Color(0.78f, 0.25f, 0.18f);

        /// <summary>Celé číslo v Animatoru vlastního modelu: stav zvířete (AnimalState: 0 toulá se, 2 utíká, 9 mrtvé…).</summary>
        public const string StateParameter = "Stav";

        private readonly Dictionary<int, View> _views = new Dictionary<int, View>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private GameConnection _connection;

        private sealed class View
        {
            public GameObject Root;
            public Renderer Body;

            /// <summary>Animace vlastního modelu (katalog vzhledu), nebo null.</summary>
            public Animator Animator;
            public Color Color;
            public AnimalState Shown = (AnimalState)255;
        }

        public void Init(GameConnection connection)
        {
            _connection = connection;
        }

        /// <summary>Počet zvířat ve scéně (pro testy).</summary>
        public int Count
        {
            get { return _views.Count; }
        }

        public static string NameOf(AnimalKind kind)
        {
            switch (kind)
            {
                case AnimalKind.Boar: return "Divočák";
                case AnimalKind.Deer: return "Jelen";
                case AnimalKind.Wolf: return "Vlk";
                default: return "Malé zvíře";
            }
        }

        private void Update()
        {
            if (_connection == null)
            {
                return;
            }

            _seen.Clear();
            foreach (var animal in _connection.Animals)
            {
                _seen.Add(animal.Id);
                View view;
                if (!_views.TryGetValue(animal.Id, out view))
                {
                    view = Create(animal);
                    _views[animal.Id] = view;
                }

                Show(view, animal);
            }

            RemoveMissing();
        }

        private void Show(View view, AnimalView animal)
        {
            var root = view.Root.transform;
            var target = WorldCoords.ToUnity(animal.X, animal.Y);
            root.position = Vector3.Lerp(root.position, target, 1 - Mathf.Exp(-Smoothing * Time.deltaTime));
            var yaw = 90f + animal.Dir * Mathf.Rad2Deg;
            if (animal.State == AnimalState.Dead)
            {
                root.rotation = Quaternion.Euler(0f, yaw, 90f);
            }
            else
            {
                root.rotation = Quaternion.Lerp(root.rotation, Quaternion.Euler(0f, yaw, 0f), 1 - Mathf.Exp(-Smoothing * Time.deltaTime));
            }

            if (animal.State == view.Shown)
            {
                return;
            }

            view.Shown = animal.State;
            if (view.Body != null)
            {
                // Tvar z kódu: spící se přikrčí, barva ukazuje nebezpečí a únavu.
                root.localScale = animal.State == AnimalState.Asleep ? new Vector3(1f, 0.6f, 1f) : Vector3.one;
                Palette.SetColor(view.Body.gameObject, ColorFor(view.Color, animal.State));
            }
            else
            {
                // Vlastní model: stav ze serveru jde do animace (parametr Stav, čísla podle AnimalState).
                AnimatorTools.SetInt(view.Animator, StateParameter, (int)animal.State);
            }
        }

        private static Color ColorFor(Color normal, AnimalState state)
        {
            switch (state)
            {
                case AnimalState.Alert:
                case AnimalState.Charge:
                    return DangerColor;
                case AnimalState.Exhaust:
                case AnimalState.Tired:
                    return Color.Lerp(normal, Color.white, 0.35f);
                case AnimalState.Dead:
                    return normal * 0.6f;
                default:
                    return normal;
            }
        }

        /// <summary>Model zvířete. Předek je na místní ose z (jako u postav), 1 dlaždice = 1 metr.</summary>
        private View Create(AnimalView animal)
        {
            var root = new GameObject(NameOf(animal.Kind) + " " + animal.Id);
            root.transform.SetParent(transform, false);
            root.transform.position = WorldCoords.ToUnity(animal.X, animal.Y);
            var prefab = VisualCatalog.Current.Animal(animal.Kind);
            if (prefab != null)
            {
                var model = VisualCatalog.Spawn(prefab, root.transform, NameOf(animal.Kind));
                return new View { Root = root, Animator = model.GetComponentInChildren<Animator>() };
            }

            switch (animal.Kind)
            {
                case AnimalKind.Rabbit:
                    return Build(root, Palette.Hex("#a08a6a"), new Vector3(0.26f, 0.22f, 0.36f), 0.14f, new Vector3(0.16f, 0.16f, 0.16f), 0f, PrimitiveType.Sphere);
                case AnimalKind.Deer:
                    return Build(root, Palette.Hex("#9b6b3c"), new Vector3(0.38f, 0.45f, 1.1f), 0.95f, new Vector3(0.22f, 0.25f, 0.35f), 0.75f, PrimitiveType.Cube);
                case AnimalKind.Wolf:
                    return Build(root, Palette.Hex("#7d7f84"), new Vector3(0.32f, 0.36f, 0.95f), 0.6f, new Vector3(0.24f, 0.24f, 0.34f), 0.42f, PrimitiveType.Cube);
                default:
                    return Build(root, Palette.Hex("#4a3a30"), new Vector3(0.55f, 0.55f, 1.0f), 0.48f, new Vector3(0.42f, 0.36f, 0.36f), 0.25f, PrimitiveType.Cube);
            }
        }

        /// <summary>Tělo ve výšce bodyHeight, hlava vpředu, a když legLength > 0, čtyři nohy.</summary>
        private static View Build(GameObject root, Color color, Vector3 body, float bodyHeight, Vector3 head, float legLength, PrimitiveType shape)
        {
            var torso = IslandView.Primitive(shape, "Tělo", root.transform, color);
            torso.transform.localPosition = new Vector3(0f, bodyHeight, 0f);
            torso.transform.localRotation = Quaternion.identity;
            torso.transform.localScale = body;

            var face = IslandView.Primitive(shape, "Hlava", root.transform, color * 0.85f);
            face.transform.localPosition = new Vector3(0f, bodyHeight + body.y * 0.35f, body.z / 2 + head.z * 0.35f);
            face.transform.localRotation = Quaternion.identity;
            face.transform.localScale = head;

            if (legLength > 0)
            {
                foreach (var side in new[] { -1f, 1f })
                {
                    foreach (var end in new[] { -1f, 1f })
                    {
                        var leg = IslandView.Primitive(PrimitiveType.Cylinder, "Noha", root.transform, color * 0.7f);
                        leg.transform.localPosition = new Vector3(side * body.x * 0.35f, legLength / 2, end * body.z * 0.38f);
                        leg.transform.localRotation = Quaternion.identity;
                        leg.transform.localScale = new Vector3(0.08f, legLength / 2, 0.08f);
                    }
                }
            }

            return new View { Root = root, Body = torso.GetComponent<Renderer>(), Color = color };
        }

        private void RemoveMissing()
        {
            var gone = new List<int>();
            foreach (var id in _views.Keys)
            {
                if (!_seen.Contains(id))
                {
                    gone.Add(id);
                }
            }

            foreach (var id in gone)
            {
                Destroy(_views[id].Root);
                _views.Remove(id);
            }
        }
    }
}
