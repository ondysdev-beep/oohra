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
            { "torch", new[] { "První pochodeň", "Světlo, které neseš s sebou. Vlci se drží dál." } },
            { "craft", new[] { "Nový recept", "Co umíš vyrobit, si pamatuješ, dokud žiješ. Najdeš to v batohu (B)." } },
            { "recovered", new[] { "Uzdravil ses", "Pamatuj si, co ti pomohlo. Nakresli si to do deníku." } },
            { "bag", new[] { "První batoh", "Teď uneseš víc než dvě věci." } },
            { "journal", new[] { "První deník", "Co si nakreslíš, přežije i tvou smrt. Kdo deník najde u těla, přečte ho." } },
            { "taught", new[] { "Někdo tě něco naučil", "Co umíš, můžeš ukázat dalším (klikni na hráče)." } },
            { "axe", new[] { "První sekera", "Teď padne i strom. Ale padá na stranu od tebe, kdo stojí za ním, dostane ránu." } },
            { "tree", new[] { "První pokácený strom", "Kmen rozsekej na klady, klad rozštípej na dřevo. Dřevo hoří dlouho." } },
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
            { "evening", "Blíží se noc. V noci je zima a chodí vlci. Nasbírej klacky u stromu a kámen na skále a rozdělej oheň (G). U ohně si zapálíš pochodeň." },
            { "tired", "Jsi unavený. Lehni si ke spánku (N), nejlépe u ohně nebo v úkrytu ze zdí." },
            { "sick", "Je ti zle. Něco z toho, co jsi jedl, ti neudělalo dobře. Pij vodu a odpočívej." },
            { "word", "Někdo poblíž mluví. Slova se objevují v zápisníku (J). Co znamenají, musíš odhadnout sám." },
            { "stranger", "Cizinci mluví svým jazykem. Sleduj, co dělají, když něco říkají. Zkus jim ukázat rostlinu." },
            { "hands", "Máš jen dvě ruce: uneseš dvě hromádky věcí. Batoh si vyrobíš. V batohu (B) zaškrtni věci a zkus je spojit." },
            { "mosquito", "Komáři mohou přenášet nemoc. Drž se u ohně (kouř), nebo se natři bahnem či popelem." },
            { "leech", "Pijavice se chytají v bažině. Sundáš je u ohně nebo popelem z ohniště." },
            { "disease", "Je ti zle. Nemoc chce svůj lék: zkoušej rostliny a zapisuj si, co pomohlo. Pij hodně vody." },
            { "storey", "Jsi v patře. Zvířata sem nedosáhnou, ze země tu ale nic nesebereš. Dolů po žebříku, nebo tlačítkem Seskočit." },
            { "roof", "Střecha musí mít dvě podpory: zdi, sloupky nebo sousední střechu. Bez podpory zapraská a spadne. Před deštěm chrání jen střecha nad hlavou." },
            { "smoke", "Oheň v jeskyni s jediným vchodem dusí. Rozdělej ho venku, nebo v jeskyni, kde je druhý východ a táhne." },
            { "cave", "Jeskyně: uvnitř je tma a stálý chlad, v zimě je tu ale tepleji než venku. Vezmi si pochodeň. Ve stěnách se najde pazourek a kape tu voda." },
            { "smoking", "Maso visí v kouři. Udí se, dokud oheň hoří (asi 4 hodiny), a pak vydrží přes týden. Na sušáku se suší ve dne, když neprší." },
            { "ticks", "V trávě a v lese se přisávají klíšťata, potichu. Občas se prohlédni (tlačítko u příznaků), ať nemoc nestihnou přenést." },
            { "snake", "Hadi leží na skalách a loukách, ve dne a v teple. Když had syčí, ustup. Jed působí se zpožděním a má svůj lék." },
            { "wasps", "Vosí hnízdo ve stromě. Vosy štípou, kdo přijde blízko. Kouř z pochodně je odežene, s ní jde hnízdo vykouřit." },
            { "fog", "Padá mlha. Ztratit se je snadné. Drž se míst, která znáš." },
            { "rain", "Prší. Mokrý promrzneš, oheň hoří rychleji. Schovej se do úkrytu ze zdí." },
            { "storm", "Bouřka. Déšť tě promáčí do kostí a pochodeň i oheň hoří rychle. Najdi úkryt." },
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
                case EventKind.Fire when (FireResult)e.Code is FireResult.TorchLit:
                    Milestone("torch");
                    break;
                case EventKind.Craft when (CraftResult)e.Code is CraftResult.Learned:
                    Milestone("craft");
                    break;
                case EventKind.Craft when (CraftResult)e.Code is CraftResult.LearnedFromOther:
                    Milestone("taught");
                    break;
                case EventKind.Wood when (WoodResult)e.Code is WoodResult.TreeFalling:
                    Milestone("tree");
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
                case EventKind.Body when (BodyEvent)e.Code is BodyEvent.MosquitoBite:
                    Tip("mosquito");
                    break;
                case EventKind.Body when (BodyEvent)e.Code is BodyEvent.Leech:
                    Tip("leech");
                    break;
                case EventKind.Climb when (ClimbResult)e.Code is ClimbResult.Up:
                    Tip("storey");
                    break;
                case EventKind.Build when (BuildResult)e.Code is BuildResult.NoSupport:
                case EventKind.Body when (BodyEvent)e.Code is BodyEvent.RoofCracking:
                    Tip("roof");
                    break;
                case EventKind.Body when (BodyEvent)e.Code is BodyEvent.Smoke:
                    Tip("smoke");
                    break;
                case EventKind.Cave when (CaveResult)e.Code is CaveResult.Entered:
                    Tip("cave");
                    break;
                case EventKind.Preserve when (PreserveResult)e.Code is PreserveResult.Hung:
                    Tip("smoking");
                    break;
                case EventKind.Body when (BodyEvent)e.Code is BodyEvent.TicksFound:
                    Tip("ticks");
                    break;
                case EventKind.Body when (BodyEvent)e.Code is BodyEvent.SnakeWarning or BodyEvent.SnakeBite:
                    Tip("snake");
                    break;
                case EventKind.Body when (BodyEvent)e.Code is BodyEvent.WaspSting:
                    Tip("wasps");
                    break;
                case EventKind.Body when (BodyEvent)e.Code is BodyEvent.Chills or BodyEvent.BellyAche or BodyEvent.AchingJoints or BodyEvent.VenomSpreads:
                    Tip("disease");
                    break;
                case EventKind.Body when (BodyEvent)e.Code is BodyEvent.Recovered:
                    Milestone("recovered");
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
            var inventory = _session.Connection.Inventory;
            if (!inventory.Bag.HasValue && inventory.Stacks.Length >= inventory.Capacity) Tip("hands");
            if (inventory.Bag.HasValue) Milestone("bag");
            if (_session.Count(ItemKind.Journal) > 0) Milestone("journal");
            if (_session.Count(ItemKind.Axe) > 0) Milestone("axe");
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
            CheckWeather();
            CheckTime();
        }

        /// <summary>Rada při první mlze, prvním dešti a první bouřce.</summary>
        private void CheckWeather()
        {
            var snapshot = _session.Connection.Snapshot;
            if (snapshot == null || snapshot.WeatherIntensity < 0.4f)
            {
                return;
            }

            if (snapshot.Weather == WeatherKind.Fog) Tip("fog");
            if (snapshot.Weather == WeatherKind.Rain) Tip("rain");
            if (snapshot.Weather == WeatherKind.Storm) Tip("storm");
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
