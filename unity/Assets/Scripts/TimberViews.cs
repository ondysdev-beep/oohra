using System.Collections.Generic;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Dřevo na zemi podle serveru (návrh 0.8, kap. 2): ležící kmeny a klady. Model z katalogu vzhledu
    /// (Log pro kmen, LogPiece pro klad: model 1 m dlouhý podél své osy X, hra ho natáhne na délku), jinak válec
    /// v barvě kůry druhu. Na dřevo jde kliknout (odseknout klad, rozštípat, vzít).
    /// </summary>
    public class TimberViews : MonoBehaviour
    {
        // Tloušťka kmene v metrech (jako trunkRadius 0,22 ve woodcutting.json).
        public const float Thickness = 0.44f;

        private readonly Dictionary<int, GameObject> _shown = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, TimberView> _views = new Dictionary<int, TimberView>();
        private GameConnection _connection;
        private TimberView[] _last;

        public int Count
        {
            get { return _shown.Count; }
        }

        /// <summary>Kmen nebo klad podle čísla (pro testy), nebo null.</summary>
        public GameObject TimberObject(int id)
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
            if (_connection == null || _connection.Timbers == _last)
            {
                return;
            }

            _last = _connection.Timbers;
            var seen = new HashSet<int>();
            foreach (var timber in _last)
            {
                seen.Add(timber.Id);
                TimberView before;
                if (_views.TryGetValue(timber.Id, out before) && before.Equals(timber))
                {
                    continue;
                }

                // Nový kus nebo změna (z kmene se odsekl klad): postaví se znovu.
                Remove(timber.Id);
                _shown[timber.Id] = Create(transform, timber);
                _views[timber.Id] = timber;
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

        /// <summary>Kmen nebo klad ležící na zemi (vodorovně ve směru Angle).</summary>
        public static GameObject Create(Transform parent, TimberView timber)
        {
            var root = new GameObject((timber.Kind == TimberKind.Trunk ? "Kmen " : "Klad ") + timber.Id);
            root.transform.SetParent(parent);
            root.transform.position = WorldCoords.ToUnity(timber.X, timber.Y, Thickness / 2);
            // Úhel na mapě (y dolů) na otočení v Unity, osa X modelu míří podél kmene.
            root.transform.rotation = Quaternion.Euler(0f, timber.Angle * Mathf.Rad2Deg, 0f);

            var prefab = timber.Kind == TimberKind.Trunk ? VisualCatalog.Current.Log : VisualCatalog.Current.LogPiece;
            if (prefab != null)
            {
                Fit(VisualCatalog.Spawn(prefab, root.transform, "Model"), timber.Length);
            }
            else
            {
                var log = IslandView.Primitive(PrimitiveType.Cylinder, "Válec", root.transform, BarkColor(timber.Tree));
                // Válec stojí podél své osy Y: položí se na osu X.
                log.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                log.transform.localPosition = Vector3.zero;
                log.transform.localScale = new Vector3(Thickness, timber.Length / 2f, Thickness);
            }

            Pickable.Attach(root, InteractionTarget.ForTimber(timber.Id, timber.X, timber.Y));
            return root;
        }

        /// <summary>
        /// Natáhne model kmene na délku (podél osy X kořene) a tloušťku Thickness. Model může ležet podél X i Z
        /// a mít jakoukoli délku (třeba 6 m kmen z Meshy): delší z vodorovných rozměrů je délka kmene.
        /// </summary>
        public static void Fit(GameObject model, float length)
        {
            var transform = model.transform;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            var size = Local(model).size;
            var alongZ = size.z > size.x;

            // Model ležící podél Z se otočí o 90°, jeho osa Z pak míří podél kmene (osa X kořene).
            transform.localRotation = alongZ ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            var lengthwise = Mathf.Max(0.01f, alongZ ? size.z : size.x);
            var across = Mathf.Max(0.01f, alongZ ? size.x : size.z);
            var thick = Thickness / across;
            var stretch = length / lengthwise;
            transform.localScale = alongZ
                ? new Vector3(thick, Thickness / Mathf.Max(0.01f, size.y), stretch)
                : new Vector3(stretch, Thickness / Mathf.Max(0.01f, size.y), thick);
            transform.localPosition = new Vector3(0f, -Thickness / 2, 0f);
        }

        /// <summary>Rozměry modelu v souřadnicích jeho rodiče (bez otočení kořene ve světě).</summary>
        private static Bounds Local(GameObject model)
        {
            var parent = model.transform.parent;
            var (position, rotation) = (parent.position, parent.rotation);
            parent.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var bounds = VisualCatalog.BoundsOf(model);
            parent.SetPositionAndRotation(position, rotation);
            return bounds;
        }

        /// <summary>Barva kůry podle druhu stromu (bříza světlá, suchý strom šedý).</summary>
        public static Color BarkColor(TreeKind tree)
        {
            switch (tree)
            {
                case TreeKind.Birch: return Palette.Hex("#d6d0c0");
                case TreeKind.Dead: return Palette.Hex("#6e6457");
                case TreeKind.Conifer: return Palette.Hex("#6a4a33");
                default: return Palette.Hex("#5a4331");
            }
        }
    }
}
