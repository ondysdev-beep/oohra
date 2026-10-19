using System.Collections.Generic;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Vosí hnízda ve stromech podle serveru: šedé hnízdo visí na větvi asi 2,6 m nad zemí (vlastní model z katalogu
    /// vzhledu, WaspNest, jinak tvar z kódu). Rozzuřené vosy krouží kolem hnízda jako roj teček. Vykouřené
    /// hnízdo zmizí. Kliká se na strom (nabídka stromu ukáže hnízdo a Vykouřit).
    /// </summary>
    public class WaspNestViews : MonoBehaviour
    {
        /// <summary>Výška hnízda nad zemí (metry).</summary>
        public const float NestHeight = 2.6f;

        private const int SwarmSize = 10;
        private const float SwarmRadius = 0.9f;

        private readonly Dictionary<int, Nest> _nests = new Dictionary<int, Nest>();
        private GameConnection _connection;
        private WaspNestView[] _shown;

        private sealed class Nest
        {
            public GameObject Root;
            public GameObject Swarm;
        }

        public void Init(GameConnection connection)
        {
            _connection = connection;
        }

        /// <summary>Kolik hnízd je vidět (pro testy).</summary>
        public int Count
        {
            get { return _nests.Count; }
        }

        /// <summary>Kolik hnízd má rozzuřený roj (pro testy).</summary>
        public int AngryCount
        {
            get
            {
                var count = 0;
                foreach (var nest in _nests.Values)
                {
                    if (nest.Swarm.activeSelf) count++;
                }

                return count;
            }
        }

        private void Update()
        {
            if (_connection == null)
            {
                return;
            }

            if (!ReferenceEquals(_connection.WaspNests, _shown))
            {
                Rebuild(_connection.WaspNests);
            }

            AnimateSwarms();
        }

        private void Rebuild(WaspNestView[] nests)
        {
            _shown = nests;
            var seen = new HashSet<int>();
            foreach (var view in nests)
            {
                seen.Add(view.TileIndex);
                Nest nest;
                if (!_nests.TryGetValue(view.TileIndex, out nest))
                {
                    nest = Create(view);
                    _nests[view.TileIndex] = nest;
                }

                nest.Swarm.SetActive(view.Angry);
            }

            var gone = new List<int>();
            foreach (var id in _nests.Keys)
            {
                if (!seen.Contains(id)) gone.Add(id);
            }

            foreach (var id in gone)
            {
                Destroy(_nests[id].Root);
                _nests.Remove(id);
            }
        }

        private Nest Create(WaspNestView view)
        {
            var root = new GameObject("Vosí hnízdo " + view.TileIndex);
            root.transform.SetParent(transform, false);
            // Hnízdo visí kousek od kmene, na straně podle čísla stromu (ať nejsou všechna stejně).
            var angle = GroundView.Hash01(view.TileIndex, 3, 29) * Mathf.PI * 2f;
            var side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.45f;
            root.transform.position = WorldCoords.ToUnity(view.X, view.Y) + side + Vector3.up * NestHeight;

            var prefab = VisualCatalog.Current.WaspNest;
            if (prefab != null)
            {
                VisualCatalog.Spawn(prefab, root.transform, "Hnízdo");
            }
            else
            {
                var stalk = IslandView.Primitive(PrimitiveType.Cylinder, "Stopka", root.transform, Palette.Hex("#4a3f33"));
                stalk.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                stalk.transform.localScale = new Vector3(0.03f, 0.1f, 0.03f);
                var body = IslandView.Primitive(PrimitiveType.Sphere, "Hnízdo", root.transform, Palette.Hex("#8a8274"));
                body.transform.localPosition = Vector3.zero;
                body.transform.localScale = new Vector3(0.28f, 0.38f, 0.28f);
            }

            var swarm = new GameObject("Roj");
            swarm.transform.SetParent(root.transform, false);
            for (var i = 0; i < SwarmSize; i++)
            {
                var wasp = IslandView.Primitive(PrimitiveType.Sphere, "Vosa", swarm.transform, Palette.Hex("#2a2418"));
                wasp.transform.localScale = Vector3.one * 0.05f;
            }

            swarm.SetActive(false);
            return new Nest { Root = root, Swarm = swarm };
        }

        /// <summary>Vosy rozzuřeného roje krouží kolem hnízda po neklidných drahách.</summary>
        private void AnimateSwarms()
        {
            var t = Time.time;
            foreach (var nest in _nests.Values)
            {
                if (!nest.Swarm.activeSelf)
                {
                    continue;
                }

                var swarm = nest.Swarm.transform;
                for (var i = 0; i < swarm.childCount; i++)
                {
                    var speed = 2.5f + i * 0.37f;
                    var a = t * speed + i * 1.7f;
                    var r = SwarmRadius * (0.5f + 0.5f * Mathf.Sin(t * 1.3f + i));
                    swarm.GetChild(i).localPosition = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a * 1.9f) * 0.5f - 0.4f, Mathf.Sin(a) * r);
                }
            }
        }
    }
}
