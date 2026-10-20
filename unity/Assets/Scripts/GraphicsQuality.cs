using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Oohra.Client
{
    /// <summary>
    /// Kvalita grafiky (F2 přepíná, pamatuje si ji PlayerPrefs): Nízká = úroveň Mobile bez úprav obrazu a s řidším
    /// okolím, Střední = úroveň PC s úpravami obrazu (Resources/OohraPost: tónování, barvy do temné nálady, záře ohně,
    /// ztmavení okrajů) a stínováním v rozích, Vysoká = navíc hustší okolí a silnější efekty. Hustota okolí se projeví
    /// při dalším načtení ostrova. V editoru se úroveň kvality projektu nemění (jen úpravy obrazu).
    /// </summary>
    public class GraphicsQuality : MonoBehaviour
    {
        public enum Level
        {
            Low = 0,
            Medium = 1,
            High = 2,
        }

        public const string PostResource = "OohraPost";
        private const string Key = "oohra.quality";

        private static Level? _current;

        /// <summary>Kvalita se změnila (F2): ozdoby okolí se rozhází znovu.</summary>
        public static event System.Action Changed;
        private Camera _camera;
        private Volume _volume;

        public static Level Current
        {
            get
            {
                if (!_current.HasValue)
                {
                    try
                    {
                        _current = (Level)Mathf.Clamp(PlayerPrefs.GetInt(Key, (int)Level.Medium), 0, 2);
                    }
                    catch (System.Exception)
                    {
                        _current = Level.Medium;
                    }
                }

                return _current.Value;
            }

            set
            {
                _current = value;
                try
                {
                    PlayerPrefs.SetInt(Key, (int)value);
                }
                catch (System.Exception)
                {
                    // Bez uložení: platí jen do konce hry.
                }
            }
        }

        /// <summary>Kolikrát víc (méně) ozdob okolí rozházet (ScatterView).</summary>
        public static float ScatterDensity
        {
            get { return Current == Level.Low ? 0.4f : Current == Level.High ? 1.6f : 1f; }
        }

        public static string NameOf(Level level)
        {
            return level == Level.Low ? "Nízká" : level == Level.High ? "Vysoká" : "Střední";
        }

        /// <summary>Úpravy obrazu jsou zapnuté (pro testy).</summary>
        public bool PostProcessing
        {
            get { return _volume != null && _volume.enabled; }
        }

        public void Init(Camera camera)
        {
            _camera = camera;
            var profile = Resources.Load<VolumeProfile>(PostResource);
            if (profile != null)
            {
                _volume = gameObject.AddComponent<Volume>();
                _volume.isGlobal = true;
                _volume.priority = 10;
                _volume.sharedProfile = profile;
            }

            Apply();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f2Key.wasPressedThisFrame && !DreamView.Active)
            {
                Current = (Level)(((int)Current + 1) % 3);
                Apply();
                Changed?.Invoke();
            }
        }

        private void Apply()
        {
            var post = Current != Level.Low;
            if (_volume != null)
            {
                _volume.enabled = post;
                _volume.weight = Current == Level.High ? 1f : 0.8f;
            }

            if (_camera != null)
            {
                var data = _camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = post;
                // Neplatný bod (NaN) z jakéhokoli shaderu by záře rozmazala do velké bílé skvrny.
                data.stopNaN = true;
                data.antialiasing = Current == Level.High ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
            }

            if (!Application.isEditor)
            {
                SetQualityLevel(Current == Level.Low ? "Mobile" : "PC");
            }
        }

        private static void SetQualityLevel(string name)
        {
            var names = QualitySettings.names;
            for (var i = 0; i < names.Length; i++)
            {
                if (names[i] == name && QualitySettings.GetQualityLevel() != i)
                {
                    QualitySettings.SetQualityLevel(i, true);
                }
            }
        }
    }
}
