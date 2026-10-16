using System;
using System.Collections.Generic;
using System.Linq;
using Oohra.Core.Language;
using Oohra.Protocol;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Oohra.Client
{
    /// <summary>
    /// Sen bez slov (návrh 4.2, prototyp drawDream): krátký film na začátku prvního života, žádný text.
    /// Čtyři scény: postava sní bobule a je jí zle, divočák vyrazí a postava ho uskočí a probodne,
    /// zimní noc a oheň, ráno u jezírka. Pradávný při tom slyší slova Starého hlasu (ne, nebezpečí, voda, ano),
    /// která zná od narození. Sen se hraje na malé scéně daleko od ostrova s vlastní kamerou. Mezerník nebo Esc ho přeskočí.
    /// </summary>
    public class DreamView : MonoBehaviour
    {
        public const float SceneSeconds = 7.5f;
        public const int SceneCount = 4;

        // Scéna snu stojí daleko od ostrova, aby z něj nebylo nic vidět.
        private static readonly Vector3 StageOrigin = new Vector3(-3000f, 0f, -3000f);
        private static readonly Color[] Backgrounds = { Palette.Hex("#10140f"), Palette.Hex("#151311"), Palette.Hex("#070913"), Palette.Hex("#1f1812") };

        private readonly List<GameObject> _sceneParts = new List<GameObject>();
        private ClientSession _session;
        private Action _onDone;
        private Transform _stage;
        private Camera _camera;
        private Camera _mainCamera;
        private Light _light;
        private GameObject _figure;
        private Color _body;
        private GameObject _boar;
        private GameObject _flame;
        private int _scene = -1;
        private float _started;
        private string _bubble;
        private Color _bubbleColor;
        private Vector3 _bubbleAt;
        private GUIStyle _bubbleStyle;

        /// <summary>Hraje se sen (rozhraní a ovládání hry počkají).</summary>
        public static bool Active { get; private set; }

        /// <summary>Testy si sen vypnou (jinak by každý nový hráč půl minuty snil).</summary>
        public static bool Disabled { get; set; }

        /// <summary>Kolikátá scéna běží (0 až 3), pro testy.</summary>
        public int Scene => _scene;

        /// <summary>Spustí sen. onDone se zavolá, když skončí nebo ho hráč přeskočí.</summary>
        public void Play(ClientSession session, Action onDone)
        {
            _session = session;
            _onDone = onDone;
            _mainCamera = Camera.main;
            BuildStage();
            Active = true;
            _started = Time.time;
            StartScene(0);
        }

        /// <summary>Konec snu (doběhl, nebo ho hráč přeskočil).</summary>
        public void Finish()
        {
            if (!Active)
            {
                return;
            }

            Active = false;
            if (_mainCamera != null)
            {
                _mainCamera.enabled = true;
            }

            Destroy(_stage.gameObject);
            var done = _onDone;
            _onDone = null;
            done?.Invoke();
            Destroy(this);
        }

        private void Update()
        {
            if (!Active)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
            {
                Finish();
                return;
            }

            var t = Time.time - _started;
            var scene = (int)(t / SceneSeconds);
            if (scene >= SceneCount)
            {
                Finish();
                return;
            }

            if (scene != _scene)
            {
                StartScene(scene);
            }

            Animate(scene, t - scene * SceneSeconds);
        }

        private void OnDestroy()
        {
            if (Active)
            {
                Active = false;
                if (_mainCamera != null)
                {
                    _mainCamera.enabled = true;
                }
            }
        }

        // ---------- scéna ----------

        private void BuildStage()
        {
            _stage = new GameObject("Sen").transform;
            _stage.position = StageOrigin;
            Part(PrimitiveType.Cylinder, "Zem", _stage, Palette.Hex("#2b2a22"), new Vector3(0f, -0.05f, 0f), new Vector3(14f, 0.05f, 9f));

            _camera = new GameObject("Kamera snu").AddComponent<Camera>();
            _camera.transform.SetParent(_stage, false);
            _camera.transform.localPosition = new Vector3(0f, 2.4f, -5.6f);
            _camera.transform.LookAt(StageOrigin + new Vector3(0f, 0.8f, 0f));
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.depth = 10;
            if (_mainCamera != null)
            {
                _mainCamera.enabled = false;
            }

            _light = new GameObject("Světlo snu").AddComponent<Light>();
            _light.type = LightType.Point;
            _light.transform.SetParent(_stage, false);
            _light.transform.localPosition = new Vector3(0f, 6f, -3f);
            _light.range = 25f;
            _light.intensity = 4f;

            var generation = _session.Connection.Self != null ? _session.Connection.Self.Generation : 0;
            _figure = GenerationLook.Figure(_stage, generation);
            _body = GenerationLook.For(generation).Body;
        }

        private void StartScene(int scene)
        {
            _scene = scene;
            foreach (var part in _sceneParts)
            {
                Destroy(part);
            }

            _sceneParts.Clear();
            _boar = null;
            _flame = null;
            _bubble = null;
            _camera.backgroundColor = Backgrounds[scene];
            _light.color = Color.white;
            _light.intensity = 4f;
            Tint(_body);
            switch (scene)
            {
                case 0:
                    // Keř s červenými bobulemi.
                    Add(PrimitiveType.Sphere, "Keř", Palette.Hex("#2f5a2a"), new Vector3(-2.6f, 0.6f, 0f), new Vector3(1.6f, 1.2f, 1.4f));
                    for (var k = 0; k < 7; k++)
                    {
                        Add(PrimitiveType.Sphere, "Bobule", Palette.Hex("#c43b3b"), new Vector3(-2.6f + Mathf.Cos(k * 2.1f) * 0.6f, 0.7f + Mathf.Sin(k * 2.1f) * 0.4f, -0.6f), Vector3.one * 0.16f);
                    }

                    break;
                case 1:
                    _boar = Add(PrimitiveType.Capsule, "Divočák", Palette.Hex("#4a3326"), new Vector3(-4f, 0.45f, 0f), new Vector3(0.7f, 0.6f, 0.7f));
                    _boar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    break;
                case 2:
                    _flame = Add(PrimitiveType.Sphere, "Plamen", Palette.Hex("#f2b03f"), new Vector3(1.2f, 0.35f, 0f), new Vector3(0.5f, 0.7f, 0.5f));
                    _flame.SetActive(false);
                    for (var k = 0; k < 24; k++)
                    {
                        // Sněhové vločky (padají, dokud je zima).
                        Add(PrimitiveType.Cube, "Vločka", Color.white, new Vector3(-4f + (k * 53 % 100) / 12.5f, 4f, -1.5f + (k * 37 % 100) / 33f), Vector3.one * 0.05f);
                    }

                    for (var k = 0; k < 6; k++)
                    {
                        var angle = k / 6f * Mathf.PI * 2;
                        Add(PrimitiveType.Sphere, "Kámen ohniště", Palette.Hex("#7c766a"), new Vector3(1.2f + Mathf.Cos(angle) * 0.55f, 0.08f, Mathf.Sin(angle) * 0.4f), Vector3.one * 0.18f);
                    }

                    break;
                default:
                    Add(PrimitiveType.Cylinder, "Jezírko", Palette.Hex("#3f7f90"), new Vector3(2.6f, 0.01f, 0.6f), new Vector3(3.2f, 0.01f, 2f));
                    Add(PrimitiveType.Sphere, "Slunce", Palette.Hex("#f2c35a"), new Vector3(-4f, 0.3f, 6f), Vector3.one * 1.2f);
                    break;
            }
        }

        private void Animate(int scene, float t)
        {
            var figure = _figure.transform;
            figure.localRotation = Quaternion.Euler(0f, -90f, 0f);
            switch (scene)
            {
                case 0:
                    // Dojde ke keři, sní bobuli a je mu zle (zezelená, vrávorá, zvrací). Zavrtí hlavou: „ne“.
                    var walk = Mathf.Clamp01(t / 1.8f);
                    figure.localPosition = new Vector3(Mathf.Lerp(2.4f, -0.9f, walk), 0f, 0f);
                    var sick = Mathf.Clamp01((t - 3f) / 1.2f);
                    if (sick > 0f)
                    {
                        Tint(Color.Lerp(_body, Palette.Hex("#8aa04a"), sick));
                        figure.localRotation = Quaternion.Euler(0f, -90f, Mathf.Sin(t * 6f) * 12f * sick);
                        if (t > 4.6f)
                        {
                            Say(Concept.No, Tone.Alarm, figure.position);
                        }
                    }

                    break;
                case 1:
                    // Divočák se zaměří a vyrazí, postava uskočí stranou a probodne ho kopím. Volá „nebezpečí“.
                    var dodge = t > 1.6f && t < 2.9f ? 1.4f : 0f;
                    figure.localPosition = new Vector3(2.4f, 0f, dodge);
                    figure.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    var run = Mathf.Clamp01(t / 2.4f);
                    var dead = t > 4.2f;
                    _boar.transform.localPosition = new Vector3(Mathf.Lerp(-4f, 4.5f, run) - (t > 2.4f ? (t - 2.4f) * 0.3f : 0f), dead ? 0.3f : 0.45f, 0f);
                    _boar.transform.localRotation = dead ? Quaternion.Euler(90f, 0f, 90f) : Quaternion.Euler(0f, 0f, 90f);
                    if (t > 1.0f && t < 2.6f)
                    {
                        Say(Concept.Danger, Tone.Alarm, figure.position);
                    }

                    break;
                case 2:
                    // Zimní noc: postava se třese a modrá zimou. Pak hoří oheň a je teplo.
                    var cold = t < 3.2f;
                    _light.color = cold ? Palette.Hex("#6f86b0") : Palette.Hex("#f2a24a");
                    _light.intensity = cold ? 1.5f : 4f;
                    var shake = cold ? Mathf.Sin(t * 40f) * 0.05f : 0f;
                    figure.localPosition = new Vector3(-0.4f + shake, 0f, 0f);
                    Tint(cold ? Palette.Hex("#9fb2c4") : _body);
                    _flame.SetActive(!cold);
                    foreach (var flake in _sceneParts.Where(p => p.name == "Vločka"))
                    {
                        var p = flake.transform.localPosition;
                        flake.SetActive(cold);
                        flake.transform.localPosition = new Vector3(p.x, 4f - ((t * 1.2f + p.x * 0.37f) % 4f), p.z);
                    }

                    if (!cold)
                    {
                        var flicker = 1f + Mathf.Sin(t * 14f) * 0.12f;
                        _flame.transform.localScale = new Vector3(0.5f * flicker, 0.7f * flicker, 0.5f * flicker);
                    }

                    break;
                default:
                    // Ráno: slunce vychází, postava dojde k jezírku a napije se. „Voda“, pak „ano“.
                    _light.color = Palette.Hex("#ffe2a8");
                    var go = Mathf.Clamp01(t / 3.2f);
                    figure.localPosition = new Vector3(Mathf.Lerp(-3f, 0.8f, go), 0f, 0.4f);
                    if (t > 3.4f && t < 5.4f)
                    {
                        Say(Concept.Water, Tone.Calm, figure.position);
                    }
                    else if (t >= 5.4f)
                    {
                        Say(Concept.Yes, Tone.Joy, figure.position);
                    }

                    break;
            }
        }

        // ---------- pomůcky ----------

        private GameObject Add(PrimitiveType type, string name, Color color, Vector3 position, Vector3 scale)
        {
            var part = Part(type, name, _stage, color, position, scale);
            _sceneParts.Add(part);
            return part;
        }

        private static GameObject Part(PrimitiveType type, string name, Transform parent, Color color, Vector3 position, Vector3 scale)
        {
            var part = IslandView.Primitive(type, name, parent, color);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = scale;
            return part;
        }

        private void Tint(Color color)
        {
            SpeciesTint.ApplyMarked(_figure, color);
        }

        /// <summary>
        /// Slovo Starého hlasu nad postavou (jen když ho hráč zná, tedy Pradávný). Slabiky bez významu, jako ve hře.
        /// </summary>
        private void Say(Concept concept, Tone tone, Vector3 at)
        {
            var entry = _session.Connection.Vocabulary.Entries.FirstOrDefault(e => e.OldVoice && e.Meaning == concept);
            if (entry == null)
            {
                _bubble = null;
                return;
            }

            if (_bubble == null)
            {
                SyllableVoice.Say(entry.Word, tone);
            }

            _bubble = Words.Text(entry.Word);
            _bubbleColor = LanguageTexts.ToneColor(tone);
            _bubbleAt = at + Vector3.up * 2.1f;
        }

        private void OnGUI()
        {
            if (!Active || _camera == null)
            {
                return;
            }

            if (_bubbleStyle == null)
            {
                _bubbleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
                _bubbleStyle.normal.textColor = Color.white;
            }

            if (_bubble != null)
            {
                var screen = _camera.WorldToScreenPoint(_bubbleAt);
                var size = _bubbleStyle.CalcSize(new GUIContent(_bubble));
                var rect = new Rect(screen.x - size.x / 2 - 10, Screen.height - screen.y - size.y, size.x + 20, size.y + 8);
                var old = GUI.color;
                GUI.color = _bubbleColor;
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = old;
                GUI.Label(rect, _bubble, _bubbleStyle);
            }

            // Tečky postupu jako v prototypu, žádný text.
            for (var k = 0; k < SceneCount; k++)
            {
                var old = GUI.color;
                GUI.color = k == _scene ? Palette.Hex("#dba23f") : Palette.Hex("#463829");
                GUI.DrawTexture(new Rect(Screen.width / 2f - 27 + k * 18, Screen.height - 72, 8, 8), Texture2D.whiteTexture);
                GUI.color = old;
            }

            GUI.Label(new Rect(Screen.width - 140, Screen.height - 40, 130, 24), "mezerník ►►", _bubbleStyle);
        }
    }
}
