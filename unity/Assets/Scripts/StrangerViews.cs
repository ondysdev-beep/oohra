using System.Collections.Generic;
using Oohra.Core.Social;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Cizinci z cizích kmenů (NPC) podle zpráv serveru. Postava jako hráč generace 2 (GenerationLook) v barvě
    /// svého kmene (prototyp: červený a modrý kmen). Kdo jí nebo pije, přikrčí se. Poloha se mezi zprávami
    /// plynule dopočítává. Na cizince jde kliknout (dát mu věc), mluví se s ním slovy a gesty.
    /// </summary>
    public class StrangerViews : MonoBehaviour
    {
        /// <summary>Jak blízko musí být cizinec, aby šlo ukázat rostlinu (askDistance v strangers.json).</summary>
        public const float AskDistance = 7f;

        private const float Smoothing = 12f;

        // Generace, jak cizinci vypadají (čelenka, štíhlá postava).
        private const int LookGeneration = 2;

        private readonly Dictionary<int, GameObject> _views = new Dictionary<int, GameObject>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private GameConnection _connection;

        public void Init(GameConnection connection)
        {
            _connection = connection;
        }

        /// <summary>Počet cizinců ve scéně (pro testy).</summary>
        public int Count
        {
            get { return _views.Count; }
        }

        /// <summary>Barva kmene (prototyp: první kmen ochrově červený, druhý modrý).</summary>
        public static Color TribeColor(int tribe)
        {
            return tribe == 0 ? Palette.Hex("#c9553a") : Palette.Hex("#4f86b3");
        }

        /// <summary>Kde cizinec ve scéně je (střed postavy, pro bubliny se slovy), nebo null.</summary>
        public Vector3? WorldPositionOf(int strangerId)
        {
            GameObject view;
            return _views.TryGetValue(strangerId, out view) ? view.transform.position + Vector3.up * (PlayerViews.BodyHeight / 2) : (Vector3?)null;
        }

        private void Update()
        {
            if (_connection == null)
            {
                return;
            }

            _seen.Clear();
            foreach (var stranger in _connection.Strangers)
            {
                _seen.Add(stranger.Id);
                GameObject view;
                if (!_views.TryGetValue(stranger.Id, out view))
                {
                    view = Create(stranger);
                    _views[stranger.Id] = view;
                }

                Show(view, stranger);
            }

            var gone = new List<int>();
            foreach (var id in _views.Keys)
            {
                if (!_seen.Contains(id)) gone.Add(id);
            }

            foreach (var id in gone)
            {
                Destroy(_views[id]);
                _views.Remove(id);
            }
        }

        private GameObject Create(StrangerView stranger)
        {
            var root = new GameObject("Cizinec " + stranger.Id);
            root.transform.SetParent(transform, false);
            root.transform.position = WorldCoords.ToUnity(stranger.X, stranger.Y);
            var figure = GenerationLook.Figure(root.transform, LookGeneration);
            SpeciesTint.ApplyMarked(figure, TribeColor(stranger.Tribe));
            Pickable.Attach(root, InteractionTarget.ForStranger(stranger.Id, stranger.X, stranger.Y));
            return root;
        }

        private static void Show(GameObject view, StrangerView stranger)
        {
            var root = view.transform;
            var blend = 1 - Mathf.Exp(-Smoothing * Time.deltaTime);
            root.position = Vector3.Lerp(root.position, WorldCoords.ToUnity(stranger.X, stranger.Y), blend);
            // Úhel na mapě (y dolů) převedený na otočení v Unity, stejně jako u hráčů.
            root.rotation = Quaternion.Lerp(root.rotation, Quaternion.Euler(0f, 90f + stranger.Facing * Mathf.Rad2Deg, 0f), blend);

            // Kdo jí nebo pije, přikrčí se.
            var figure = root.Find(GenerationLook.FigureName);
            if (figure != null)
            {
                var look = GenerationLook.For(LookGeneration);
                var crouch = stranger.State == StrangerState.Eat ? 0.75f : 1f;
                figure.localScale = new Vector3(look.Width, look.Height * crouch, look.Width);
            }
        }
    }
}
