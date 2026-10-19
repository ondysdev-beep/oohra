using System.Collections.Generic;
using Oohra.Core.Plants;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Maso v kouři a na sušácích podle serveru. Nad ohněm stojí trojnožka z klacků a pod jejím vrcholem visí kusy
    /// masa dokola, na sušáku visí v řadě podél jeho osy. Vzhled kusu je v katalogu (MeatStrip), jinak plochý
    /// kvádr. Barva ukazuje stav: syrové červené, jak se udí nebo suší, tmavne, hotové je tmavě hnědé.
    /// </summary>
    public class RackViews : MonoBehaviour
    {
        /// <summary>Výška vrcholu trojnožky nad ohněm (metry).</summary>
        public const float TripodHeight = 1.5f;

        /// <summary>Výška, ve které visí maso na sušáku.</summary>
        public const float RackHeight = 1.0f;

        private static readonly Color RawColor = new Color(0.62f, 0.2f, 0.18f);
        private static readonly Color SmokedColor = new Color(0.3f, 0.17f, 0.1f);
        private static readonly Color DriedColor = new Color(0.42f, 0.26f, 0.16f);

        private readonly List<GameObject> _shown = new List<GameObject>();
        private GameConnection _connection;
        private RackView[] _racks;
        private FireView[] _fires;
        private StructureView[] _structures;

        public void Init(GameConnection connection)
        {
            _connection = connection;
        }

        /// <summary>Kolik kusů masa je vidět (pro testy).</summary>
        public int PieceCount { get; private set; }

        private void Update()
        {
            if (_connection == null
                || (ReferenceEquals(_connection.Racks, _racks) && ReferenceEquals(_connection.Fires, _fires) && ReferenceEquals(_connection.Structures, _structures)))
            {
                return;
            }

            _racks = _connection.Racks;
            _fires = _connection.Fires;
            _structures = _connection.Structures;
            Rebuild();
        }

        /// <summary>Barva kusu: syrový přechází podle postupu do barvy hotového.</summary>
        public static Color ColorOf(RackHolder holder, RackPieceView piece)
        {
            if (piece.Cooking == Cooking.Smoked) return SmokedColor;
            if (piece.Cooking == Cooking.Dried) return DriedColor;
            var target = holder == RackHolder.Fire ? SmokedColor : DriedColor;
            return Color.Lerp(RawColor, target, piece.Progress / 255f * 0.8f);
        }

        private void Rebuild()
        {
            foreach (var go in _shown)
            {
                Destroy(go);
            }

            _shown.Clear();
            PieceCount = 0;
            foreach (var rack in _racks)
            {
                Vector3 position;
                float angle;
                if (!Place(rack, out position, out angle))
                {
                    continue;
                }

                var root = new GameObject("Maso " + rack.Holder + " " + rack.Id);
                root.transform.SetParent(transform, false);
                root.transform.position = position;
                root.transform.rotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
                _shown.Add(root);
                if (rack.Holder == RackHolder.Fire)
                {
                    AddTripod(root.transform);
                }

                for (var i = 0; i < rack.Pieces.Length; i++)
                {
                    AddPiece(root.transform, PieceSpot(rack.Holder, i, rack.Pieces.Length), ColorOf(rack.Holder, rack.Pieces[i]));
                    PieceCount++;
                }
            }
        }

        /// <summary>Kde kus visí vůči ohni nebo sušáku (místní souřadnice).</summary>
        public static Vector3 PieceSpot(RackHolder holder, int index, int count)
        {
            if (holder == RackHolder.Fire)
            {
                var a = index * Mathf.PI * 2f / Mathf.Max(1, count);
                return new Vector3(Mathf.Cos(a) * 0.12f, TripodHeight - 0.35f, Mathf.Sin(a) * 0.12f);
            }

            return new Vector3((index - (count - 1) / 2f) * 0.13f, RackHeight, 0f);
        }

        private bool Place(RackView rack, out Vector3 position, out float angle)
        {
            position = Vector3.zero;
            angle = 0f;
            if (rack.Holder == RackHolder.Fire)
            {
                foreach (var fire in _fires)
                {
                    if (fire.Id == rack.Id)
                    {
                        position = WorldCoords.ToUnity(fire.X, fire.Y);
                        return true;
                    }
                }

                return false;
            }

            foreach (var structure in _structures)
            {
                if (structure.Id == rack.Id)
                {
                    position = WorldCoords.ToUnity(structure.X, structure.Y);
                    angle = structure.Angle;
                    return true;
                }
            }

            return false;
        }

        private static void AddTripod(Transform root)
        {
            for (var i = 0; i < 3; i++)
            {
                var a = i * Mathf.PI * 2f / 3f;
                var foot = new Vector3(Mathf.Cos(a) * 0.55f, 0f, Mathf.Sin(a) * 0.55f);
                var top = new Vector3(0f, TripodHeight, 0f);
                var stick = IslandView.Primitive(PrimitiveType.Cylinder, "Klacek", root, Palette.Hex("#5a4632"));
                stick.transform.localPosition = (foot + top) / 2f;
                stick.transform.localRotation = Quaternion.FromToRotation(Vector3.up, top - foot);
                stick.transform.localScale = new Vector3(0.035f, (top - foot).magnitude / 2f, 0.035f);
            }
        }

        private static void AddPiece(Transform root, Vector3 spot, Color color)
        {
            var prefab = VisualCatalog.Current.MeatStrip;
            GameObject piece;
            if (prefab != null)
            {
                piece = new GameObject("Kus masa");
                piece.transform.SetParent(root, false);
                VisualCatalog.Spawn(prefab, piece.transform, "Model", center: false);
                foreach (var renderer in piece.GetComponentsInChildren<Renderer>())
                {
                    renderer.material.color = color;
                }
            }
            else
            {
                piece = IslandView.Primitive(PrimitiveType.Cube, "Kus masa", root, color);
                piece.transform.localScale = new Vector3(0.08f, 0.24f, 0.03f);
            }

            piece.transform.localPosition = spot;
            piece.transform.localRotation = Quaternion.identity;
        }
    }
}
