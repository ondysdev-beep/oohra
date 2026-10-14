using System.Collections.Generic;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Těla mrtvých hráčů ve scéně: postava ležící na zemi, vypadá podle generace jako za života (GenerationLook),
    /// jen tmavší. Na tělo jde kliknout a vzít si věci (CorpseHud). Po 4 dnech zmizí.
    /// </summary>
    public class CorpseViews : MonoBehaviour
    {
        private readonly Dictionary<int, GameObject> _views = new Dictionary<int, GameObject>();
        private GameConnection _connection;
        private CorpseView[] _shown;

        public void Init(GameConnection connection)
        {
            _connection = connection;
        }

        /// <summary>Počet těl ve scéně (pro testy).</summary>
        public int Count
        {
            get { return _views.Count; }
        }

        private void Update()
        {
            if (_connection == null)
            {
                return;
            }

            HideWhileOwnerLies();
            if (ReferenceEquals(_connection.Corpses, _shown))
            {
                return;
            }

            _shown = _connection.Corpses;
            var current = new HashSet<int>();
            foreach (var corpse in _shown)
            {
                current.Add(corpse.Id);
                if (!_views.ContainsKey(corpse.Id))
                {
                    _views[corpse.Id] = Create(corpse);
                }
            }

            var gone = new List<int>();
            foreach (var id in _views.Keys)
            {
                if (!current.Contains(id)) gone.Add(id);
            }

            foreach (var id in gone)
            {
                Destroy(_views[id]);
                _views.Remove(id);
            }
        }

        /// <summary>Dokud se mrtvý znovu nenarodí, leží na místě jeho postava: tělo by tam bylo podruhé.</summary>
        private void HideWhileOwnerLies()
        {
            var snapshot = _connection.Snapshot;
            if (snapshot == null || _shown == null)
            {
                return;
            }

            foreach (var corpse in _shown)
            {
                GameObject view;
                if (!_views.TryGetValue(corpse.Id, out view))
                {
                    continue;
                }

                var ownerLies = false;
                foreach (var player in snapshot.Players)
                {
                    ownerLies |= player.Id == corpse.OwnerId && player.Dead;
                }

                if (view.activeSelf == ownerLies)
                {
                    view.SetActive(!ownerLies);
                }
            }
        }

        private GameObject Create(CorpseView corpse)
        {
            var root = new GameObject("Tělo " + corpse.Id);
            root.transform.SetParent(transform, false);
            root.transform.position = WorldCoords.ToUnity(corpse.X, corpse.Y);
            // Natočení podle čísla těla, ať všechna neleží stejně.
            root.transform.rotation = Quaternion.Euler(0f, corpse.Id * 67 % 360, 0f);

            // Postava podle generace (jako živá), ležící na zádech: hlava míří k -Z, nohy na +Z, střed těla uprostřed.
            var figure = GenerationLook.Figure(root.transform, corpse.Generation);
            var look = GenerationLook.For(corpse.Generation);
            var lift = GenerationLook.ModelFor(corpse.Generation) != null ? 0.15f : 0.28f * look.Width;
            figure.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            figure.transform.localPosition = new Vector3(0f, lift, PlayerViews.BodyHeight * look.Height / 2);
            SpeciesTint.ApplyMarked(figure, look.Body * 0.8f);

            Pickable.Attach(root, InteractionTarget.ForCorpse(corpse.Id, corpse.X, corpse.Y));
            return root;
        }
    }
}
