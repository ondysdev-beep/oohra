using System.Collections.Generic;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Vchody do jeskyní, které moje postava zná (server posílá jen viditelné a objevené nenápadné). Venku je ve
    /// skále tmavý otvor (katalog CaveEntrance, nenápadný HiddenCaveEntrance), uvnitř u východu sloup denního
    /// světla shora (katalog CaveExit). Na obojí jde kliknout: Vstoupit / Vyjít ven.
    /// </summary>
    public class CaveEntranceViews : MonoBehaviour
    {
        private readonly List<GameObject> _shown = new List<GameObject>();
        private GameConnection _connection;
        private CaveEntranceView[] _entrances;

        public void Init(GameConnection connection)
        {
            _connection = connection;
        }

        /// <summary>Kolik vchodů je vidět (venku, bez východů uvnitř; pro testy).</summary>
        public int Count { get; private set; }

        private void Update()
        {
            if (_connection == null || ReferenceEquals(_connection.CaveEntrances, _entrances))
            {
                return;
            }

            _entrances = _connection.CaveEntrances;
            foreach (var go in _shown)
            {
                Destroy(go);
            }

            _shown.Clear();
            foreach (var entrance in _entrances)
            {
                _shown.Add(CreateOutside(entrance));
                _shown.Add(CreateInside(entrance));
            }

            Count = _entrances.Length;
        }

        /// <summary>Otvor ve skále: stojí na hraně dlaždice směrem ke skále a dívá se ven.</summary>
        private GameObject CreateOutside(CaveEntranceView entrance)
        {
            var root = new GameObject((entrance.Hidden ? "Nenápadný vchod " : "Vchod do jeskyně ") + entrance.Id);
            root.transform.SetParent(transform, false);
            var face = new Vector3(Mathf.Cos(entrance.Facing), 0f, -Mathf.Sin(entrance.Facing));
            root.transform.position = WorldCoords.ToUnity(entrance.X, entrance.Y) + face * 0.55f;
            root.transform.rotation = Quaternion.LookRotation(-face, Vector3.up);
            var catalog = VisualCatalog.Current;
            var prefab = entrance.Hidden && catalog.HiddenCaveEntrance != null ? catalog.HiddenCaveEntrance : catalog.CaveEntrance;
            if (prefab != null)
            {
                VisualCatalog.Spawn(prefab, root.transform, "Model");
            }
            else
            {
                var rock = IslandView.Primitive(PrimitiveType.Cube, "Skála", root.transform, Palette.Hex("#6f6a60"));
                rock.transform.localPosition = new Vector3(0f, 1.1f, -0.3f);
                rock.transform.localScale = new Vector3(entrance.Hidden ? 1.6f : 2.4f, 2.2f, 0.6f);
                var hole = IslandView.Primitive(PrimitiveType.Cube, "Otvor", root.transform, new Color(0.03f, 0.03f, 0.04f));
                hole.transform.localPosition = new Vector3(0f, entrance.Hidden ? 0.55f : 0.8f, 0.01f);
                hole.transform.localScale = entrance.Hidden ? new Vector3(0.5f, 1.1f, 0.05f) : new Vector3(1.2f, 1.6f, 0.05f);
            }

            Pickable.Attach(root, InteractionTarget.ForCave(entrance.Id, entrance.X, entrance.Y));
            return root;
        }

        /// <summary>Východ uvnitř: světlo, které padá shora (je vidět i ve tmě jeskyně).</summary>
        private GameObject CreateInside(CaveEntranceView entrance)
        {
            var root = new GameObject("Východ z jeskyně " + entrance.Id);
            root.transform.SetParent(transform, false);
            root.transform.position = WorldCoords.ToUnity(entrance.InsideX, entrance.InsideY);
            var prefab = VisualCatalog.Current.CaveExit;
            if (prefab != null)
            {
                VisualCatalog.Spawn(prefab, root.transform, "Model");
            }
            else
            {
                var shaft = IslandView.Primitive(PrimitiveType.Cylinder, "Světlo shora", root.transform, Color.white);
                shaft.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                shaft.transform.localScale = new Vector3(1.1f, 1.6f, 1.1f);
                Palette.SetFade(shaft, new Color(0.85f, 0.9f, 0.8f, 0.18f));
            }

            var light = new GameObject("Světlo").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 1.2f;
            light.color = new Color(0.8f, 0.86f, 0.9f);
            Pickable.Attach(root, InteractionTarget.ForCave(entrance.Id, entrance.InsideX, entrance.InsideY));
            return root;
        }
    }
}
