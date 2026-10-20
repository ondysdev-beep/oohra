using UnityEngine;
using UnityEngine.InputSystem;

namespace Oohra.Client
{
    /// <summary>
    /// Kamera z třetí osoby: pořád za postavou, dívá se jí přes rameno dopředu. A a D otáčí postavou i kamerou
    /// (kamera tak zůstává za zády), W a S jde dopředu a dozadu, Q a E úkrok (PlayerController). Držením pravého
    /// (nebo prostředního) tlačítka myši se jde rozhlížet: do stran otáčí, nahoru a dolů mění sklon. Kolečko
    /// přibližuje. Při chůzi kliknutím se kamera sama stočí za postavu. Pod zem kamera neklesne.
    /// V režimu stavby Q a E otáčejí stavbou, ne úkrokem (KeysTurnCamera).
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] private float _pitch = 16f;
        [SerializeField] private float _minPitch = -10f;
        [SerializeField] private float _maxPitch = 70f;
        [SerializeField] private float _distance = 6f;
        [SerializeField] private float _minDistance = 2.5f;
        [SerializeField] private float _maxDistance = 14f;
        [SerializeField] private float _keyTurnSpeed = 110f;
        [SerializeField] private float _mouseTurnSpeed = 0.25f;
        [SerializeField] private float _mousePitchSpeed = 0.18f;
        [SerializeField] private float _followSpeed = 12f;

        // Kam se kamera dívá: kousek nad hlavou a trochu vpravo (přes rameno).
        private const float LookHeight = 1.7f;
        private const float ShoulderOffset = 0.45f;

        // Jak rychle se kamera sama stočí za postavu, když jde kliknutím (stupně za sekundu, podle odchylky).
        private const float AutoTurn = 3f;

        // Nejmenší výška kamery nad terénem (metry).
        private const float GroundClearance = 0.5f;

        private Transform _target;
        private float _yaw;
        private Vector3 _focus;
        private Vector3 _lastTarget;

        /// <summary>Q a E dělají úkrok. Režim stavby si je bere pro otáčení stavby.</summary>
        public static bool KeysTurnCamera { get; set; } = true;

        /// <summary>Kterým směrem se kamera dívá (stupně, 0 = sever v Unity, osa +z).</summary>
        public float Yaw
        {
            get { return _yaw; }
        }

        public void Init(Transform target)
        {
            _target = target;
            _focus = target.position;
            _lastTarget = target.position;
            Apply();
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            var mouseLook = ReadMouse();
            ReadKeys();
            if (!mouseLook)
            {
                FollowWalking();
            }

            // Po skoku (vchod do jeskyně) kamera přeskočí hned za postavou.
            _focus = (_target.position - _focus).sqrMagnitude > PlayerViews.TeleportDistance * PlayerViews.TeleportDistance
                ? _target.position
                : Vector3.Lerp(_focus, _target.position, 1 - Mathf.Exp(-_followSpeed * Time.deltaTime));
            _lastTarget = _target.position;
            Apply();
        }

        /// <summary>A a D otáčí postavou i kamerou (kamera je pořád za zády).</summary>
        private void ReadKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || DreamView.Active)
            {
                return;
            }

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) _yaw -= _keyTurnSpeed * Time.deltaTime;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) _yaw += _keyTurnSpeed * Time.deltaTime;
        }

        /// <summary>Rozhlížení myší (držet pravé nebo prostřední tlačítko) a přiblížení kolečkem. Vrací, jestli se rozhlíží.</summary>
        private bool ReadMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            var looking = mouse.middleButton.isPressed || mouse.rightButton.isPressed;
            if (looking)
            {
                var delta = mouse.delta.ReadValue();
                _yaw += delta.x * _mouseTurnSpeed;
                _pitch = Mathf.Clamp(_pitch - delta.y * _mousePitchSpeed, _minPitch, _maxPitch);
            }

            var scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0)
            {
                _distance = Mathf.Clamp(_distance * (scroll > 0 ? 0.9f : 1.1f), _minDistance, _maxDistance);
            }

            return looking;
        }

        /// <summary>
        /// Postava jde (kliknutím nebo dopředu): kamera se plynule stočí za ni. Při couvání (S) a úkroku se nestáčí.
        /// </summary>
        private void FollowWalking()
        {
            var moved = _target.position - _lastTarget;
            moved.y = 0f;
            if (moved.magnitude < 0.01f || moved.magnitude > 1f)
            {
                return;
            }

            var heading = Mathf.Atan2(moved.x, moved.z) * Mathf.Rad2Deg;
            var difference = Mathf.DeltaAngle(_yaw, heading);
            if (Mathf.Abs(difference) < 100f)
            {
                _yaw += difference * Mathf.Clamp01(AutoTurn * Time.deltaTime);
            }
        }

        private void Apply()
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var look = _focus + Vector3.up * LookHeight + rotation * Vector3.right * ShoulderOffset;
            var position = look - rotation * Vector3.forward * _distance;

            // Pod zem kamera neklesne (svah za postavou): zvedne se nad terén.
            double x, y;
            WorldCoords.FromUnity(position, out x, out y);
            var floor = WorldCoords.Ground(x, y) + GroundClearance;
            if (position.y < floor)
            {
                position.y = floor;
            }

            transform.position = position;
            transform.rotation = Quaternion.LookRotation(look - position, Vector3.up);
        }
    }
}
