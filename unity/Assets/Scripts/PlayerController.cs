using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Oohra.Client
{
    /// <summary>
    /// Ovládání postavy: WASD (vzhledem ke kameře), kliknutí na zem (jdi tam),
    /// kliknutí na rostlinu nebo vodu (dojdi k ní a otevři nabídku).
    /// Kam se smí jít a jak rychle, rozhoduje Core (Movement). Unity jen zobrazuje.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        private const float StuckSeconds = 0.45f;
        private const double PickRadius = 0.6;
        private const double MenuCloseExtra = 1.2;

        private GameState _state;
        private Character _character;
        private Camera _camera;
        private double _x;
        private double _y;
        private Vector3? _moveTo;
        private InteractionTarget _target;
        private float _stuckTime;

        public double X => _x;

        public double Y => _y;

        public void Init(GameState state, Camera camera)
        {
            _state = state;
            _camera = camera;
            Respawn(state.Character, state.BirthPlace);
        }

        public void Respawn(Character character, Position place)
        {
            _character = character;
            _x = place.X;
            _y = place.Y;
            _moveTo = null;
            _target = null;
            transform.position = WorldCoords.ToUnity(_x, _y, 0.6f);
        }

        private void Update()
        {
            if (_state == null || _state.Paused || _character.FaintMinutes > 0)
            {
                return;
            }

            ReadClick();
            var direction = KeyboardDirection();
            if (direction != Vector3.zero)
            {
                _moveTo = null;
                _target = null;
                _state.Menu = null;
            }
            else if (_target != null)
            {
                direction = DirectionToTarget();
            }
            else if (_moveTo.HasValue)
            {
                direction = DirectionToPoint(_moveTo.Value);
            }

            if (direction != Vector3.zero)
            {
                Move(direction);
            }

            CloseMenuIfWalkedAway();
        }

        /// <summary>Kliknutí: na rostlinu nebo vodu nastaví cíl, jinak jde postava na místo.</summary>
        private void ReadClick()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || GameHud.MouseOverHud)
            {
                return;
            }

            var ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out var distance))
            {
                return;
            }

            var point = ray.GetPoint(distance);
            WorldCoords.FromUnity(point, out var x, out var y);
            _state.Menu = null;
            _stuckTime = 0;
            _target = PickTarget(x, y);
            _moveTo = _target == null ? point : (Vector3?)null;
        }

        private InteractionTarget PickTarget(double x, double y)
        {
            var island = _state.Island;
            var now = _state.Clock.TotalMinutes;
            var best = PickRadius;
            PlantInstance? found = null;
            foreach (var plant in island.Plants)
            {
                var d = Distance(plant.X, plant.Y, x, y);
                if (d < best && !_state.PlantStates.IsGone(plant.Id, now))
                {
                    best = d;
                    found = plant;
                }
            }

            if (found.HasValue)
            {
                return InteractionTarget.ForPlant(found.Value);
            }

            var tx = (int)System.Math.Floor(x);
            var ty = (int)System.Math.Floor(y);
            if (island.InBounds(tx, ty) && Drinking.IsWater(island.TileAt(tx, ty)))
            {
                return InteractionTarget.ForWater(tx, ty);
            }

            return null;
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

        /// <summary>Jde k cíli. Když je dost blízko, otevře nabídku (jako v prototypu).</summary>
        private Vector3 DirectionToTarget()
        {
            var reach = Reach(_target);
            WorldCoords.FromUnity(_target.Position, out var tx, out var ty);
            if (Distance(tx, ty, _x, _y) <= reach - 0.15)
            {
                _state.Menu = _target;
                _target = null;
                return Vector3.zero;
            }

            return DirectionToPoint(_target.Position);
        }

        private Vector3 DirectionToPoint(Vector3 point)
        {
            var offset = point - transform.position;
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
            var distance = Movement.Distance(_state.Island, _character, _x, _y, Time.deltaTime);
            var before = new Vector2((float)_x, (float)_y);
            var result = Movement.Step(_state.Island, _x, _y, direction.x, -direction.z, distance);
            _x = result.X;
            _y = result.Y;

            transform.position = WorldCoords.ToUnity(_x, _y, 0.6f);
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            StopIfStuck(before, distance);
        }

        /// <summary>Když postava při chůzi narazí na překážku, po chvíli to vzdá.</summary>
        private void StopIfStuck(Vector2 before, double distance)
        {
            if (!_moveTo.HasValue && _target == null)
            {
                return;
            }

            var moved = Vector2.Distance(before, new Vector2((float)_x, (float)_y));
            _stuckTime = moved < distance * 0.3 ? _stuckTime + Time.deltaTime : 0;
            if (_stuckTime > StuckSeconds)
            {
                _moveTo = null;
                _target = null;
                _stuckTime = 0;
            }
        }

        private void CloseMenuIfWalkedAway()
        {
            var menu = _state.Menu;
            if (menu == null)
            {
                return;
            }

            WorldCoords.FromUnity(menu.Position, out var tx, out var ty);
            if (Distance(tx, ty, _x, _y) > Reach(menu) + MenuCloseExtra)
            {
                _state.Menu = null;
            }
        }

        private static double Reach(InteractionTarget target)
        {
            var reach = GameData.Gathering.Reach;
            return target.IsPlant ? reach.Plant : reach.Water;
        }

        private static double Distance(double ax, double ay, double bx, double by)
        {
            var dx = ax - bx;
            var dy = ay - by;
            return System.Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
