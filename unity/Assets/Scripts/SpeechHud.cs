using System.Collections.Generic;
using System.Linq;
using Oohra.Core.Language;
using Oohra.Protocol;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Oohra.Client
{
    /// <summary>
    /// Řeč ze slabik a gesta (návrh 9): panel řeči (T), gesta (V), zápisník slov (J) a bubliny nad hráči.
    /// Hra přenáší jen slabiky a náladu. Co slovo znamená, si hráč zapisuje sám a hra mu to nepotvrdí.
    /// </summary>
    public class SpeechHud : MonoBehaviour
    {
        private const float BaseHeight = 900f;
        private const float WordSeconds = 3.2f;
        private const float GestureSeconds = 2.2f;
        private const float PointSeconds = 3f;
        private const float PointDistance = 3f;

        private static readonly List<Rect> _rects = new List<Rect>();
        private static readonly Color Ochre = new Color(0.86f, 0.64f, 0.25f);

        private readonly Dictionary<int, Bubble> _bubbles = new Dictionary<int, Bubble>();
        private readonly List<PointMark> _points = new List<PointMark>();
        private ClientSession _session;
        private PlayerViews _players;
        private StrangerViews _strangers;
        private Camera _camera;
        private GUIStyle _label;
        private GUIStyle _title;
        private GUIStyle _bubble;
        private GUIStyle _small;
        private GUIStyle _hint;
        private Vector2 _scroll;
        private string _chosenWord;

        /// <summary>Rozepsané slovo v panelu řeči.</summary>
        public SpeechInput Input { get; } = new SpeechInput();

        public Tone Tone { get; set; }

        public bool SpeechOpen { get; set; }

        public bool GesturesOpen { get; set; }

        public bool NotebookOpen { get; set; }

        /// <summary>Kolik bublin je teď vidět (pro testy).</summary>
        public int VisibleBubbles
        {
            get { return _bubbles.Values.Count(b => b.Until >= Time.time); }
        }

        public void Init(ClientSession session, PlayerViews players, Camera camera)
        {
            _session = session;
            _players = players;
            _camera = camera;
        }

        /// <summary>Bubliny cizinců se kreslí nad jejich postavami.</summary>
        public void ShowStrangers(StrangerViews strangers)
        {
            _strangers = strangers;
        }

        /// <summary>Klíč bubliny: hráč má své číslo, cizinec záporné (aby se čísla nepletla).</summary>
        private static int BubbleKey(int id, bool stranger)
        {
            return stranger ? -id - 1 : id;
        }

        private Vector3? BubblePosition(int key)
        {
            if (key >= 0)
            {
                return _players.WorldPositionOf(key);
            }

            return _strangers != null ? _strangers.WorldPositionOf(-key - 1) : null;
        }

        /// <summary>Je myš nad některým panelem řeči (souřadnice rozhraní)? Klik pak nemá posílat postavu jinam.</summary>
        public static bool Covers(Vector2 mouse)
        {
            return _rects.Any(r => r.Contains(mouse));
        }

        /// <summary>Přidá slabiku do rozepsaného slova a potichu ji přehraje (hráč slyší, co skládá).</summary>
        private void AddSyllable(byte syllable)
        {
            var before = Input.Count;
            Input.Add(syllable);
            if (Input.Count > before)
            {
                SyllableVoice.Say(new[] { syllable }, Tone);
            }
        }

        /// <summary>Řekne rozepsané slovo a vyprázdní ho.</summary>
        public void SayWord()
        {
            if (Input.Count == 0)
            {
                return;
            }

            _session.Connection.Say(Input.ToArray(), Tone);
            Input.Clear();
        }

        /// <summary>Udělá gesto. Ukázat míří na vybranou věc (kruh), jinak kousek před postavu.</summary>
        public void MakeGesture(Gesture gesture)
        {
            var place = Vector2.zero;
            if (gesture == Gesture.Point)
            {
                var self = _session.Connection.SelfSnapshot();
                if (_session.Selected != null)
                {
                    place = _session.PositionOf(_session.Selected);
                }
                else if (self.HasValue)
                {
                    place = new Vector2(self.Value.X + Mathf.Cos(self.Value.Facing) * PointDistance, self.Value.Y + Mathf.Sin(self.Value.Facing) * PointDistance);
                }
            }

            _session.Connection.MakeGesture(gesture, place.x, place.y);
        }

        private void Update()
        {
            // Během snu bez slov (nováček) se rozhraní neukazuje a hra nereaguje na ovládání.
            if (DreamView.Active)
            {
                return;
            }

            if (_session == null)
            {
                return;
            }

            TakeServerMessages();
            var keyboard = Keyboard.current;
            if (keyboard == null || _session.Dead)
            {
                return;
            }

            if (keyboard.tKey.wasPressedThisFrame) SpeechOpen = !SpeechOpen;
            if (keyboard.vKey.wasPressedThisFrame) GesturesOpen = !GesturesOpen;
            if (keyboard.jKey.wasPressedThisFrame) NotebookOpen = !NotebookOpen;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                SpeechOpen = GesturesOpen = NotebookOpen = false;
            }

            if (SpeechOpen)
            {
                ReadSpeechKeys(keyboard);
            }
        }

        private void ReadSpeechKeys(Keyboard keyboard)
        {
            if (keyboard.shiftKey.isPressed)
            {
                var digits = new[]
                {
                    keyboard.digit0Key, keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key, keyboard.digit4Key,
                    keyboard.digit5Key, keyboard.digit6Key, keyboard.digit7Key, keyboard.digit8Key, keyboard.digit9Key,
                };
                for (var digit = 0; digit < digits.Length; digit++)
                {
                    if (digits[digit].wasPressedThisFrame) AddSyllable(SpeechInput.SyllableOfDigit(digit));
                }
            }

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) SayWord();
            if (keyboard.backspaceKey.wasPressedThisFrame) Input.RemoveLast();
            if (keyboard.tabKey.wasPressedThisFrame) Tone = (Tone)(((int)Tone + 1) % 3);
        }

        /// <summary>Slova a gesta od serveru se ukážou jako bubliny nad postavou.</summary>
        private void TakeServerMessages()
        {
            var connection = _session.Connection;
            foreach (var speech in connection.TakeSpeech())
            {
                var text = Words.Text(speech.Word);
                var meaning = MeaningOf(speech.Word);
                if (meaning.HasValue && (speech.Stranger || speech.PlayerId != connection.PlayerId))
                {
                    text += "\n(" + LanguageTexts.ConceptName(meaning.Value) + "?)";
                }

                var key = BubbleKey(speech.PlayerId, speech.Stranger);
                _bubbles[key] = new Bubble(text, LanguageTexts.ToneColor(speech.Tone), Time.time + WordSeconds);
                // Slovo je slyšet u toho, kdo ho řekl (zvuk slabik je stejný pro všechny, návrh 9.2).
                SyllableVoice.Say(speech.Word, speech.Tone, BubblePosition(key));
            }

            foreach (var gesture in connection.TakeGestures())
            {
                // U ukázané věci bublina řekne, co to je (stejně jako v batohu: list, plod, maso...).
                var bubble = LanguageTexts.GestureBubble(gesture.Gesture);
                if (gesture.Item.HasValue)
                {
                    bubble += ": " + Texts.Describe(gesture.Item.Value).ToLower();
                }

                _bubbles[BubbleKey(gesture.PlayerId, gesture.Stranger)] = new Bubble(bubble, LanguageTexts.ToneColor(Tone.Calm), Time.time + GestureSeconds);
                if (gesture.Gesture == Gesture.Point)
                {
                    _points.Add(new PointMark(WorldCoords.ToUnity(gesture.X, gesture.Y), Time.time + PointSeconds));
                }
            }

            _points.RemoveAll(p => p.Until < Time.time);
        }

        /// <summary>Význam, který si hráč ke slovu zapsal, nebo null.</summary>
        private Concept? MeaningOf(byte[] word)
        {
            var entry = FindEntry(Words.Key(word));
            return entry != null ? entry.Meaning : null;
        }

        private WordEntryView FindEntry(string key)
        {
            return _session.Connection.Vocabulary.Entries.FirstOrDefault(e => Words.Key(e.Word) == key);
        }

        private void OnGUI()
        {
            // Během snu bez slov (nováček) se rozhraní neukazuje a hra nereaguje na ovládání.
            if (DreamView.Active)
            {
                return;
            }

            if (_session == null || _session.Connection.Self == null)
            {
                return;
            }

            CreateStyles();
            DrawBubbles();
            var scale = Mathf.Clamp(Screen.height / BaseHeight, 1f, 2f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            var height = Screen.height / scale;

            if (Event.current.type == EventType.Layout)
            {
                _rects.Clear();
            }

            if (_session.Dead)
            {
                return;
            }

            if (_session.BackpackOpen)
            {
                NotebookOpen = false;
            }

            var bottom = height - 86;
            if (SpeechOpen)
            {
                bottom = DrawSpeech(width, bottom);
            }

            if (GesturesOpen)
            {
                DrawGestures(width, bottom);
            }

            if (NotebookOpen)
            {
                DrawNotebook(width, height);
            }
        }

        /// <summary>Bubliny se slovy a gesty nad hlavami a značky, kam někdo ukazuje (v bodech obrazovky).</summary>
        private void DrawBubbles()
        {
            GUI.matrix = Matrix4x4.identity;
            foreach (var pair in _bubbles)
            {
                var position = BubblePosition(pair.Key);
                if (pair.Value.Until < Time.time || !position.HasValue)
                {
                    continue;
                }

                var screen = _camera.WorldToScreenPoint(position.Value + Vector3.up * (PlayerViews.BodyHeight / 2 + 0.8f));
                if (screen.z < 0)
                {
                    continue;
                }

                var content = new GUIContent(pair.Value.Text);
                var size = _bubble.CalcSize(content);
                var rect = new Rect(screen.x - size.x / 2 - 8, Screen.height - screen.y - size.y - 6, size.x + 16, size.y + 8);
                var old = GUI.color;
                GUI.color = pair.Value.Color;
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = old;
                GUI.Label(rect, content, _bubble);
            }

            foreach (var point in _points)
            {
                var screen = _camera.WorldToScreenPoint(point.Position + Vector3.up * 0.6f);
                if (screen.z >= 0)
                {
                    GUI.Label(new Rect(screen.x - 20, Screen.height - screen.y - 20, 40, 40), "▼", _bubble);
                }
            }
        }

        /// <summary>Panel řeči vpravo dole: slabiky, rozepsané slovo, Říct, Zopakovat a nálada. Vrací horní okraj.</summary>
        private float DrawSpeech(float width, float bottom)
        {
            var rect = new Rect(width - 452, bottom - 128, 440, 128);
            Panel(rect);
            var x = rect.x + 10;
            var y = rect.y + 6;
            GUI.Label(new Rect(x, y, 200, 22), "ŘEČ", _title);
            if (GUI.Button(new Rect(rect.xMax - 78, y, 68, 22), "Zavřít")) SpeechOpen = false;
            y += 26;

            for (var i = 0; i < Words.Syllables.Length; i++)
            {
                var digit = (i + 1) % 10;
                if (GUI.Button(new Rect(x + i * 42, y, 40, 30), Words.Syllables[i] + "\n" + digit, _small))
                {
                    AddSyllable((byte)i);
                }
            }

            y += 36;
            GUI.Label(new Rect(x, y, 200, 24), Input.Text, Input.Count == 0 ? _hint : _title);
            if (GUI.Button(new Rect(x + 206, y, 70, 24), "Říct")) SayWord();
            if (GUI.Button(new Rect(x + 282, y, 60, 24), "Smazat")) Input.RemoveLast();
            if (GUI.Button(new Rect(x + 348, y, 72, 24), LanguageTexts.ToneName(Tone))) Tone = (Tone)(((int)Tone + 1) % 3);
            y += 28;

            var last = _session.Connection.Vocabulary.LastHeard;
            if (last.Length > 0 && GUI.Button(new Rect(x, y, 200, 22), "Zopakovat: " + Words.Text(last)))
            {
                Input.Set(last);
            }

            GUI.Label(new Rect(x + 206, y, 220, 22), "Shift + číslo, Enter, Tab nálada", _hint);
            return rect.y - 6;
        }

        /// <summary>Gesta: rozumí jim každý, i ten, kdo nezná žádné slovo.</summary>
        private void DrawGestures(float width, float bottom)
        {
            var gestures = new[] { Gesture.Yes, Gesture.No, Gesture.Come, Gesture.Beware, Gesture.Eat, Gesture.Drink, Gesture.Point };
            var rect = new Rect(width - 452, bottom - 108, 440, 108);
            Panel(rect);
            var x = rect.x + 10;
            var y = rect.y + 6;
            GUI.Label(new Rect(x, y, 200, 22), "GESTA", _title);
            GUI.Label(new Rect(x + 70, y + 2, 290, 20), "Ukázat míří na věc v kroužku", _hint);
            if (GUI.Button(new Rect(rect.xMax - 78, y, 68, 22), "Zavřít")) GesturesOpen = false;
            y += 28;
            for (var i = 0; i < gestures.Length; i++)
            {
                var cell = new Rect(x + (i % 4) * 106, y + (i / 4) * 34, 100, 28);
                if (GUI.Button(cell, LanguageTexts.GestureName(gestures[i]), _small))
                {
                    MakeGesture(gestures[i]);
                }
            }
        }

        /// <summary>
        /// Zápisník slov (prototyp záložka Slovník): co jsi slyšel, kolikrát, v jaké situaci a co si myslíš, že to znamená.
        /// Smrtí se ztratí.
        /// </summary>
        private void DrawNotebook(float width, float height)
        {
            var vocabulary = _session.Connection.Vocabulary;
            var rect = new Rect((width - 640) / 2, 110, 640, Mathf.Min(height - 230, 560));
            Panel(rect);
            var x = rect.x + 12;
            var y = rect.y + 8;
            GUI.Label(new Rect(x, y, 200, 24), "ZÁPISNÍK SLOV", _title);
            GUI.Label(new Rect(x + 150, y + 2, 360, 22), "Význam si zapiš sám, hra ti ho nepotvrdí.", _hint);
            if (GUI.Button(new Rect(rect.xMax - 90, y, 78, 24), "Zavřít")) NotebookOpen = false;
            y += 32;

            var chosen = _chosenWord != null ? FindEntry(_chosenWord) : null;
            var pickerHeight = chosen != null ? 112f : 0f;
            var listRect = new Rect(x, y, rect.width - 24, rect.yMax - y - pickerHeight - 8);
            if (vocabulary.Entries.Length == 0)
            {
                GUI.Label(listRect, "Zatím jsi žádné slovo neslyšel. Poslouchej, co si lidé říkají (a co přitom dělají).", _label);
                return;
            }

            const float rowHeight = 44f;
            var content = new Rect(0, 0, listRect.width - 20, vocabulary.Entries.Length * rowHeight);
            _scroll = GUI.BeginScrollView(listRect, _scroll, content);
            for (var i = 0; i < vocabulary.Entries.Length; i++)
            {
                DrawEntry(vocabulary.Entries[i], new Rect(0, i * rowHeight, content.width, rowHeight - 4));
            }

            GUI.EndScrollView();
            if (chosen != null)
            {
                DrawMeaningPicker(chosen, new Rect(x, rect.yMax - pickerHeight - 4, rect.width - 24, pickerHeight));
            }
        }

        private void DrawEntry(WordEntryView entry, Rect row)
        {
            var key = Words.Key(entry.Word);
            var meaning = entry.Meaning.HasValue ? LanguageTexts.ConceptName(entry.Meaning.Value) : "? neznámé";
            GUI.Label(new Rect(row.x, row.y, 150, 22), Words.Text(entry.Word), _title);
            GUI.Label(new Rect(row.x, row.y + 20, row.width - 230, 22), LanguageTexts.Describe(entry), _hint);
            if (GUI.Button(new Rect(row.xMax - 224, row.y + 4, 150, 28), (key == _chosenWord ? "▲ " : "▼ ") + meaning, _small))
            {
                _chosenWord = key == _chosenWord ? null : key;
            }

            if (GUI.Button(new Rect(row.xMax - 66, row.y + 4, 62, 28), "Říct", _small))
            {
                Input.Set(entry.Word);
                SpeechOpen = true;
            }
        }

        /// <summary>Výběr významu pro slovo: 16 pojmů a „nevím“.</summary>
        private void DrawMeaningPicker(WordEntryView entry, Rect rect)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 20), "Co podle tebe znamená " + Words.Text(entry.Word) + "?", _label);
            var concepts = (Concept[])System.Enum.GetValues(typeof(Concept));
            var cellWidth = (rect.width - 8 * 4) / 9;
            for (var i = 0; i <= concepts.Length; i++)
            {
                var cell = new Rect(rect.x + (i % 9) * (cellWidth + 4), rect.y + 24 + (i / 9) * 32, cellWidth, 28);
                var concept = i < concepts.Length ? concepts[i] : (Concept?)null;
                var text = concept.HasValue ? LanguageTexts.ConceptName(concept.Value) : "nevím";
                if (GUI.Button(cell, entry.Meaning == concept ? "• " + text : text, _small))
                {
                    _session.Connection.SetMeaning(entry.Word, concept);
                    _chosenWord = null;
                }
            }
        }

        private void Panel(Rect rect)
        {
            GUI.Box(rect, GUIContent.none);
            GUI.Box(rect, GUIContent.none);
            if (Event.current.type == EventType.Layout)
            {
                _rects.Add(rect);
            }
        }

        private void CreateStyles()
        {
            if (_label != null)
            {
                return;
            }

            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            _label.normal.textColor = new Color(0.94f, 0.9f, 0.82f);
            _title = new GUIStyle(_label) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = false };
            _title.normal.textColor = Ochre;
            _small = new GUIStyle(GUI.skin.button) { fontSize = 12, wordWrap = false };
            _hint = new GUIStyle(_label) { fontSize = 12, wordWrap = false };
            _bubble = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false };
            _bubble.normal.textColor = Color.white;
        }

        private sealed class Bubble
        {
            public Bubble(string text, Color color, float until)
            {
                Text = text;
                Color = color;
                Until = until;
            }

            public string Text { get; }

            public Color Color { get; }

            public float Until { get; }
        }

        private sealed class PointMark
        {
            public PointMark(Vector3 position, float until)
            {
                Position = position;
                Until = until;
            }

            public Vector3 Position { get; }

            public float Until { get; }
        }
    }
}
