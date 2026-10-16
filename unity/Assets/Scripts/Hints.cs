using System.Collections.Generic;
using System.Linq;
using Oohra.Core;
using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.Social;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Pomoc nováčkům (návrh 4.2: svět vede hráče sám, první hodina má být úspěšná; prototyp hint a milestone):
    /// krátké rady ve chvíli, kdy se hodí (žízeň, hlad, večer, únava, první otrava, první slovo, cizinec), a oslava
    /// „poprvé“ (první oheň, kopí, úlovek, přežitá noc…). Každá rada a oslava jen jednou za hráče, pamatuje si je
    /// tento počítač (PlayerPrefs). Nic neprozrazují o rostlinách ani o významu slov.
    /// </summary>
    public class Hints : MonoBehaviour
    {
        private const float BannerSeconds = 5f;
        private const float BaseHeight = 900f;

        /// <summary>Oslavy „poprvé“ (prototyp MS): nadpis a věta.</summary>
        public static readonly Dictionary<string, string[]> Milestones = new Dictionary<string, string[]>
        {
            { "fire", new[] { "První oheň", "Teplo a světlo. Noc je o něco méně strašná." } },
            { "spear", new[] { "První kopí", "Ostrý kámen na klacku mění lov." } },
            { "food", new[] { "První jídlo", "Snědl jsi něco a zatím ti není zle. Zapamatuj si to, otrava může přijít později." } },
            { "poison", new[] { "První otrava", "Takhle se učí hranice. Vzpomeň si, co jsi jedl." } },
            { "hunt", new[] { "První úlovek", "Maso je síla. Nenech ho dlouho ležet." } },
            { "fish", new[] { "První ryba", "Voda dává i jídlo." } },
            { "mark", new[] { "První značka", "Kdo přijde po tobě, přečte si, co jsi věděl." } },
            { "shelter", new[] { "První úkryt", "Uzavřený prostor hřeje a chrání ve spánku." } },
            { "word", new[] { "První slovo", "Neznáš jeho význam. Sleduj, co dělají, když ho říkají." } },
            { "gift", new[] { "První dar od cizinců", "Kdo se naučí jazyk, získá přátele." } },
            { "tribe", new[] { "Kmen", "Už nejsi sám. Zásoby, stavby a těla kmene jsou i tvoje." } },
            { "night", new[] { "Přežil jsi noc", "Je ráno." } },
            { "winter", new[] { "Přežil jsi zimu", "Přichází jaro." } },
        };

        /// <summary>Rady ve chvíli, kdy se hodí.</summary>
        public static readonly Dictionary<string, string> Tips = new Dictionary<string, string>
        {
            { "thirst", "Máš žízeň. Klikni na jezírko (světlejší modrá) a napij se. Moře je slané." },
            { "hunger", "Máš hlad. Klikni na rostlinu a utrhni list, plod nebo vykopej kořen. Nejdřív jen ochutnej (B batoh)." },
            { "backpack", "V batohu (B) můžeš věc ochutnat. Chuť napoví, ale nic nezaručí. Otrava přichází se zpožděním." },
            { "evening", "Blíží se noc. V noci je zima a chodí vlci. Nasbírej klacky u stromu a kámen na skále a rozdělej oheň (G)." },
            { "tired", "Jsi unavený. Lehni si ke spánku (N), nejlépe u ohně nebo v úkrytu ze zdí." },
            { "sick", "Je ti zle. Něco z toho, co jsi jedl, ti neudělalo dobře. Pij vodu a odpočívej." },
            { "word", "Někdo poblíž mluví. Slova se objevují v zápisníku (J). Co znamenají, musíš odhadnout sám." },
            { "stranger", "Cizinci mluví svým jazykem. Sleduj, co dělají, když něco říkají. Zkus jim ukázat rostlinu." },
            { "animal", "Zvíře poblíž. Divočák se rozběhne, když se k němu přiblížíš: uhni na stranu a bij, když je vyčerpaný." },
        };

        private readonly HashSet<string> _seen = new HashSet<string>();
        private ClientSession _session;
        private string _prefsKey;
        private string _bannerTitle;
        private string _bannerText;
        private float _bannerUntil;
        private float _nextCheck;
        private int _lastDay = -1;
        private Season? _lastSeason;
        private GUIStyle _title;
        private GUIStyle _text;

        public void Init(ClientSession session, string nickname)
        {
            _session = session;
            _prefsKey = "Oohra.Hints." + nickname;
            foreach (var key in PlayerPrefs.GetString(_prefsKey, string.Empty).Split(','))
            {
                if (key.Length > 0) _seen.Add(key);
            }

            _session.EventReceived += OnEvent;
        }

        /// <summary>Už to hráč viděl (rada nebo oslava)?</summary>
        public bool Seen(string key)
        {
            return _seen.Contains(key);
        }

        /// <summary>Právě zobrazená oslava (pro testy), nebo null.</summary>
        public string Banner => Time.time < _bannerUntil ? _bannerTitle : null;

        /// <summary>Rada (jednou za hráče) do hlášek.</summary>
        public void Tip(string key)
        {
            if (Remember("tip." + key))
            {
                _session.Say(Tips[key], false);
            }
        }

        /// <summary>Oslava „poprvé“ (jednou za hráče): nápis nahoře uprostřed a zvuk.</summary>
        public void Milestone(string key)
        {
            if (!Remember("ms." + key))
            {
                return;
            }

            _bannerTitle = Milestones[key][0];
            _bannerText = Milestones[key][1];
            _bannerUntil = Time.time + BannerSeconds;
            Chime.Play();
        }

        private bool Remember(string key)
        {
            if (!_seen.Add(key))
            {
                return false;
            }

            PlayerPrefs.SetString(_prefsKey, string.Join(",", _seen));
            return true;
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.EventReceived -= OnEvent;
            }
        }

        /// <summary>Hlášky ze serveru, které znamenají „poprvé“.</summary>
        private void OnEvent(GameEvent e)
        {
            switch (e.Kind)
            {
                case EventKind.Fire when (FireResult)e.Code is FireResult.Lit:
                    Milestone("fire");
                    break;
                case EventKind.Hunt when (HuntResult)e.Code is HuntResult.SpearMade:
                    Milestone("spear");
                    break;
                case EventKind.Hunt when (HuntResult)e.Code is HuntResult.Killed or HuntResult.CaughtSmall:
                    Milestone("hunt");
                    break;
                case EventKind.Hunt when (HuntResult)e.Code is HuntResult.FishCaught:
                    Milestone("fish");
                    break;
                case EventKind.Sign when (SignResult)e.Code is SignResult.Drawn:
                    Milestone("mark");
                    break;
                case EventKind.Stranger when (StrangerResult)e.Code is StrangerResult.Gift:
                    Milestone("gift");
                    break;
                case EventKind.Tribe when (TribeResult)e.Code is TribeResult.Joined or TribeResult.TheyJoined:
                    Milestone("tribe");
                    break;
                case EventKind.Body when (BodyEvent)e.Code is BodyEvent.Ate:
                    Milestone("food");
                    break;
                case EventKind.Body when (BodyEvent)e.Code is BodyEvent.Vomited or BodyEvent.Diarrhea or BodyEvent.Tremor or BodyEvent.Fainted:
                    Milestone("poison");
                    Tip("sick");
                    break;
            }
        }

        private void Update()
        {
            if (_session == null || DreamView.Active || Time.time < _nextCheck)
            {
                return;
            }

            _nextCheck = Time.time + 0.5f;
            var self = _session.Connection.Self;
            if (self == null || self.Dead)
            {
                return;
            }

            if (self.Hydration < 45) Tip("thirst");
            if (self.Satiety < 45) Tip("hunger");
            if (self.Energy < 25) Tip("tired");
            if (self.Sheltered) Milestone("shelter");
            if (_session.Connection.Inventory.Stacks.Any(s => s.Kind == ItemKind.Plant)) Tip("backpack");
            if (_session.Connection.Vocabulary.Entries.Any(e => !e.OldVoice && e.Heard > 0))
            {
                Tip("word");
                Milestone("word");
            }

            if (_session.NearestStranger(12f).HasValue) Tip("stranger");
            if (AnimalNear(8f)) Tip("animal");
            CheckTime();
        }

        /// <summary>Večer rada, ráno oslava přežité noci, na jaře oslava přežité zimy.</summary>
        private void CheckTime()
        {
            var clock = _session.Clock;
            if (clock.Hour >= 18 && clock.Hour < 21) Tip("evening");
            if (_lastDay >= 0 && clock.Day > _lastDay && clock.Hour >= 6) Milestone("night");
            if (clock.Hour >= 6) _lastDay = clock.Day;
            if (_lastSeason == Season.Winter && clock.Season == Season.Spring) Milestone("winter");
            _lastSeason = clock.Season;
        }

        private bool AnimalNear(float distance)
        {
            var self = _session.Connection.SelfSnapshot();
            return self.HasValue && _session.Connection.Animals.Any(a =>
                a.Kind != AnimalKind.Rabbit && Vector2.Distance(new Vector2(a.X, a.Y), new Vector2(self.Value.X, self.Value.Y)) < distance);
        }

        private void OnGUI()
        {
            if (DreamView.Active || Banner == null)
            {
                return;
            }

            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _title.normal.textColor = new Color(0.86f, 0.64f, 0.25f);
                _text = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                _text.normal.textColor = new Color(0.94f, 0.9f, 0.82f);
            }

            var scale = Mathf.Clamp(Screen.height / BaseHeight, 1f, 2f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            var rect = new Rect((width - 460) / 2, 70, 460, 74);
            var old = GUI.color;
            GUI.color = new Color(0.1f, 0.08f, 0.06f, 0.85f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(rect.x, rect.y + 6, rect.width, 28), _bannerTitle, _title);
            GUI.Label(new Rect(rect.x + 12, rect.y + 34, rect.width - 24, 36), _bannerText, _text);
        }
    }
}
