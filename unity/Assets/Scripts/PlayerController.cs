using Oohra.Core.Characters;
using Oohra.Core.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Oohra.Client
{
    /// <summary>
    /// Ovládání postavy: WASD (vzhledem ke kameře) nebo kliknutí myší na zem.
    /// Kam se smí jít a jak rychle, rozhoduje Core (Movement). Unity jen zobrazuje.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        private const float StuckSeconds = 0.45f;

        private Island _island;
        private Character _character;
        private Camera _camera;
        private double _x;
        private double _y;
        private Vector3? _moveTo;
        private float _stuckTime;

        public double X => _x;

        public double Y => _y;

        public void Init(Island island, Character character, Position start, Camera camera)
        {
            _island = island;
            _character = character;
            _camera = camera;
            _x = start.X;
            _y = start.Y;
            transform.position = WorldCoords.ToUnity(_x, _y, 0.6f);
        }

        private void Update()
        {
            if (_island == null || _character.IsDead || _character.FaintMinutes > 0)
            {
                return;
            }

            ReadClick();
            var direction = KeyboardDirection();
            if (direction != Vector3.zero)
            {
                _moveTo = null;
            }
            else if (_moveTo.HasValue)
            {
                direction = DirectionToTarget(_moveTo.Value);
            }

            if (direction != Vector3.zero)
            {
                Move(direction);
            }
        }

        private void ReadClick()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            var ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out var distance))
            {
                _moveTo = ray.GetPoint(distance);
                _stuckTime = 0;
            }
        }

        /// <summary>WASD a šipky. „Nahoru“ je směr, kam se dívá kamera.</summary>
        private Vector3 KeyboardDirection()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector3.zero;
            }

            var input = Vector2.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1;
            if (input == Vector2.zero)
            {
                return Vector3.zero;
            }

            var forward = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(_camera.transform.right, Vector3.up).normalized;
            return (forward * input.y + right * input.x).normalized;
        }

        private Vector3 DirectionToTarget(Vector3 target)
        {
            var offset = target - transform.position;
            offset.y = 0;
            if (offset.magnitude < 0.15f)
            {
                _moveTo = null;
                return Vector3.zero;
            }

            return offset.normalized;
        }

        private void Move(Vector3 direction)
        {
            // Unity z roste na sever, y hry na jih, proto -direction.z.
            var distance = Movement.Distance(_island, _character, _x, _y, Time.deltaTime);
            var before = new Vector2((float)_x, (float)_y);
            var result = Movement.Step(_island, _x, _y, direction.x, -direction.z, distance);
            _x = result.X;
            _y = result.Y;

            transform.position = WorldCoords.ToUnity(_x, _y, 0.6f);
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            StopIfStuck(before, distance);
        }

        /// <summary>Když postava při chůzi na kliknuté místo narazí na překážku, po chvíli to vzdá.</summary>
        private void StopIfStuck(Vector2 before, double distance)
        {
            if (!_moveTo.HasValue)
            {
                return;
            }

            var moved = Vector2.Distance(before, new Vector2((float)_x, (float)_y));
            _stuckTime = moved < distance * 0.3 ? _stuckTime + Time.deltaTime : 0;
            if (_stuckTime > StuckSeconds)
            {
                _moveTo = null;
                _stuckTime = 0;
            }
        }
    }
}
