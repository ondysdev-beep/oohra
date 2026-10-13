using System.Collections.Generic;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Všichni hráči ze snímků serveru jako kapsle. Server posílá polohy asi 15× za sekundu,
    /// mezi nimi se postavy plynule posouvají, aby pohyb nebyl trhaný.
    /// </summary>
    public class PlayerViews : MonoBehaviour
    {
        private const float Smoothing = 15f;

        // 1 dlaždice = 1 metr. Postava je vysoká 1,8 m a široká jako kolize na serveru (radius 0,28 v movement.json).
        public const float BodyHeight = 1.8f;
        private const float BodyRadius = 0.28f;

        private readonly Dictionary<int, GameObject> _views = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, int> _shownGeneration = new Dictionary<int, int>();
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
                var target = WorldCoords.ToUnity(player.X, player.Y, player.Dead ? BodyRadius : BodyHeight / 2);
                view.transform.position = Vector3.Lerp(view.transform.position, target, 1 - Mathf.Exp(-Smoothing * Time.deltaTime));
                // Úhel na mapě (y dolů) převedený na otočení v Unity (z nahoru).
                var facing = Quaternion.Euler(0f, 90f + player.Facing * Mathf.Rad2Deg, 0f);
                view.transform.rotation = player.Dead ? Quaternion.Euler(90f, 0f, 0f) : facing;
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

            view = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            view.name = id == _connection.PlayerId ? "Postava" : "Hráč " + id;
            view.transform.SetParent(transform);
            view.transform.localScale = new Vector3(BodyRadius * 2, BodyHeight / 2, BodyRadius * 2);
            Destroy(view.GetComponent<Collider>());

            // Nos ukazuje, kam se postava dívá.
            var nose = IslandView.Primitive(PrimitiveType.Cube, "Směr", view.transform, Palette.Hex("#dba23f"));
            nose.transform.localPosition = new Vector3(0f, 0.5f, 0.5f);
            nose.transform.localScale = new Vector3(0.3f, 0.15f, 0.4f);

            Paint(view, generation);
            _shownGeneration[id] = generation;
            _views[id] = view;
            if (id == _connection.PlayerId)
            {
                Self = view.transform;
            }

            return view;
        }

        /// <summary>Pradávní jsou tmavší (kožešina), Noví světlejší.</summary>
        private static void Paint(GameObject view, int generation)
        {
            Palette.SetColor(view, generation == 0 ? Palette.Hex("#8a6a45") : Palette.Hex("#c5a97e"));
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
            }
        }
    }
}
