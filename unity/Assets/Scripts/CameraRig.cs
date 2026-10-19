using UnityEngine;
using UnityEngine.InputSystem;

namespace Oohra.Client
{
    /// <summary>
    /// Kamera shora, která sleduje postavu. Držením Q a E se plynule otáčí. S drženým kolečkem myši
    /// pohyb myši nahoru a dolů mění sklon, do stran otáčí. Otáčení kolečkem přibližuje.
    /// V režimu stavby Q a E otáčejí stavbou, ne kamerou (KeysTurnCamera).
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] private float _pitch = 55f;
        [SerializeField] private float _minPitch = 20f;
        [SerializeField] private float _maxPitch = 85f;
        [SerializeField] private float _distance = 18f;
        [SerializeField] private float _minDistance = 6f;
        [SerializeField] private float _maxDistance = 40f;
        [SerializeField] private float _keyTurnSpeed = 120f;
        [SerializeField] private float _mouseTurnSpeed = 0.3f;
        [SerializeField] private float _mousePitchSpeed = 0.2f;
        [SerializeField] private float _followSpeed = 10f;

        private Transform _target;
        private float _yaw;
        private Vector3 _focus;

        /// <summary>Q a E otáčejí kamerou. Režim stavby si je bere pro otáčení stavby.</summary>
        public static bool KeysTurnCamera { get; set; } = true;

        public void Init(Transform target)
        {
            _target = target;
            _focus = target.position;
            Apply();
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            ReadKeys();
            ReadMouse();
            // Po skoku (vchod do jeskyně) kamera přeskočí hned za postavou.
            _focus = (_target.position - _focus).sqrMagnitude > PlayerViews.TeleportDistance * PlayerViews.TeleportDistance
                ? _target.position
                : Vector3.Lerp(_focus, _target.position, 1 - Mathf.Exp(-_followSpeed * Time.deltaTime));
            Apply();
        }

        private void ReadKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !KeysTurnCamera)
            {
                return;
            }

            if (keyboard.qKey.isPressed) _yaw -= _keyTurnSpeed * Time.deltaTime;
            if (keyboard.eKey.isPressed) _yaw += _keyTurnSpeed * Time.deltaTime;
        }

        private void ReadMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (mouse.middleButton.isPressed)
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
        }

        private void Apply()
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.rotation = rotation;
            transform.position = _focus - rotation * Vector3.forward * _distance;
        }
    }
}
