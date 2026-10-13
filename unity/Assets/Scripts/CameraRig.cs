using UnityEngine;
using UnityEngine.InputSystem;

namespace Oohra.Client
{
    /// <summary>
    /// Kamera shora s pevným sklonem (návrh 15): sleduje postavu, Q a E ji otočí o 90°, kolečko myši přiblíží.
    /// Volné otáčení při stavění přijde se stavěním.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] private float _pitch = 55f;
        [SerializeField] private float _distance = 16f;
        [SerializeField] private float _minDistance = 7f;
        [SerializeField] private float _maxDistance = 32f;
        [SerializeField] private float _turnSpeed = 8f;
        [SerializeField] private float _followSpeed = 10f;

        private Transform _target;
        private float _yaw;
        private float _targetYaw;
        private Vector3 _focus;

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

            ReadInput();
            _yaw = Mathf.LerpAngle(_yaw, _targetYaw, 1 - Mathf.Exp(-_turnSpeed * Time.deltaTime));
            _focus = Vector3.Lerp(_focus, _target.position, 1 - Mathf.Exp(-_followSpeed * Time.deltaTime));
            Apply();
        }

        private void ReadInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.qKey.wasPressedThisFrame) _targetYaw -= 90f;
                if (keyboard.eKey.wasPressedThisFrame) _targetYaw += 90f;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                var scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0)
                {
                    _distance = Mathf.Clamp(_distance * (scroll > 0 ? 0.9f : 1.1f), _minDistance, _maxDistance);
                }
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
