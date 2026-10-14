using System.Collections.Generic;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Hromady kamenů na skalách podle serveru: každý kámen v hromadě je jeden model z katalogu vzhledu (Rocks).
    /// Dole je kruh až čtyř velkých kamenů, nahoře menší. Když hráč kámen vezme, zmizí ten nejvyšší,
    /// prázdná hromada zmizí celá. Na hromadu jde kliknout (Vzít kámen).
    /// </summary>
    public class RockPileViews : MonoBehaviour
    {
        private const int BottomPieces = 4;
        private const float RingRadius = 0.2f;

        private readonly Dictionary<int, Pile> _piles = new Dictionary<int, Pile>();
        private GameConnection _connection;
        private WorldView _world;
        private RockPileView[] _shown;

        private sealed class Pile
        {
            public GameObject Root;
            public readonly List<GameObject> Pieces = new List<GameObject>();
        }

        public void Init(GameConnection connection, WorldView world)
        {
            _connection = connection;
            _world = world;
        }

        /// <summary>Kolik kamenů je teď vidět na všech hromadách dohromady (pro testy).</summary>
        public int VisibleStones
        {
            get
            {
                var count = 0;
                foreach (var pile in _piles.Values)
                {
                    foreach (var piece in pile.Pieces)
                    {
                        if (piece.activeSelf && pile.Root.activeSelf) count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Rozložení kamenů v hromadě (poloha vůči středu dlaždice a velikost). První jsou spodní, poslední vrchní:
        /// v tomhle pořadí se kameny při těžbě odebírají od konce.
        /// </summary>
        public static List<KeyValuePair<Vector3, float>> Layout(int stones, int seed, float minSize, float maxSize)
        {
            var pieces = new List<KeyValuePair<Vector3, float>>();
            var bottom = Mathf.Min(stones, BottomPieces);
            var turn = GroundView.Hash01(seed, 1, 17) * 360f;
            for (var k = 0; k < bottom; k++)
            {
                var angle = (turn + k * 360f / bottom) * Mathf.Deg2Rad;
                var radius = bottom == 1 ? 0f : RingRadius;
                var size = Mathf.Lerp(Mathf.Lerp(minSize, maxSize, 0.6f), maxSize, GroundView.Hash01(seed, k + 2, 17));
                pieces.Add(new KeyValuePair<Vector3, float>(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius), size));
            }

            for (var k = bottom; k < stones; k++)
            {
                // Vrchní kameny leží na spodních, trochu do strany, ať je hromada vidět.
                var angle = (turn + 45f + k * 137f) * Mathf.Deg2Rad;
                var size = Mathf.Lerp(minSize, Mathf.Lerp(minSize, maxSize, 0.5f), GroundView.Hash01(seed, k + 2, 17));
                var height = maxSize * (0.35f + 0.25f * (k - bottom));
                pieces.Add(new KeyValuePair<Vector3, float>(new Vector3(Mathf.Cos(angle) * 0.08f, height, Mathf.Sin(angle) * 0.08f), size));
            }

            return pieces;
        }

        private void Update()
        {
            if (_connection == null || ReferenceEquals(_connection.RockPiles, _shown))
            {
                return;
            }

            _shown = _connection.RockPiles;
            foreach (var view in _shown)
            {
                Pile pile;
                if (!_piles.TryGetValue(view.TileIndex, out pile))
                {
                    pile = Create(view);
                    _piles[view.TileIndex] = pile;
                }

                pile.Root.SetActive(view.Stones > 0);
                for (var i = 0; i < pile.Pieces.Count; i++)
                {
                    pile.Pieces[i].SetActive(i < view.Stones);
                }
            }
        }

        private Pile Create(RockPileView view)
        {
            var catalog = VisualCatalog.Current;
            var tx = view.TileIndex % _world.Width;
            var ty = view.TileIndex / _world.Width;
            var pile = new Pile { Root = new GameObject("Skála " + tx + "," + ty) };
            pile.Root.transform.SetParent(transform, false);
            pile.Root.transform.position = WorldCoords.ToUnity(tx + 0.5, ty + 0.5);

            var models = catalog.Rocks ?? new GameObject[0];
            var layout = Layout(view.MaxStones, view.TileIndex, catalog.RockMinSize, catalog.RockMaxSize);
            for (var k = 0; k < layout.Count; k++)
            {
                var place = layout[k].Key;
                var size = layout[k].Value;
                var rotation = Quaternion.Euler(0f, GroundView.Hash01(view.TileIndex, k + 20, 17) * 360f, 0f);
                var model = models.Length > 0 ? models[(view.TileIndex * 7 + k) % models.Length] : null;
                GameObject piece;
                if (model != null)
                {
                    piece = VisualCatalog.Spawn(model, pile.Root.transform, "Kámen " + (k + 1), largest: size);
                    piece.transform.localPosition = place;
                }
                else
                {
                    var grey = Palette.Tile(TileType.Rock, false) * Mathf.Lerp(0.75f, 1.05f, GroundView.Hash01(view.TileIndex, k + 30, 17));
                    grey.a = 1f;
                    piece = IslandView.Primitive(PrimitiveType.Sphere, "Kámen " + (k + 1), pile.Root.transform, grey);
                    piece.transform.localPosition = place + Vector3.up * size * 0.3f;
                    piece.transform.localScale = new Vector3(size, size * 0.65f, size * 0.85f);
                }

                piece.transform.localRotation = rotation;
                pile.Pieces.Add(piece);
            }

            Pickable.Attach(pile.Root, InteractionTarget.ForTile(TargetKind.Rock, tx, ty));
            return pile;
        }
    }
}
