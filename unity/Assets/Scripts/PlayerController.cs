using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Oohra.Client
{
    /// <summary>
    /// Ovládání postavy: WASD (vzhledem ke kameře), kliknutí na zem (jdi tam),
    /// kliknutí na rostlinu nebo vodu (dojdi k ní a otevři nabídku).
    /// Klient posílá serveru jen směr chůze. Kam se smí jít, rozhoduje server (Movement v Core).
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        // Dosah jako v Core (gathering.json). Server ho stejně ověří, tady jen kvůli tomu, kdy přestat jít.
        private const double PlantReach = 0.95;
        private const double WaterReach = 1.7;
        private const double PickRadius = 0.6;
        private const double MenuCloseExtra = 1.2;
        private const float StuckSeconds = 0.8f;

        private ClientSession _session;
        private Camera _camera;
        private Vector2? _moveTo;
        private InteractionTarget _target;
        private Vector2 _lastPosition;
        private Vector2 _sent;
        private float _stuckTime;

        public void Init(ClientSession session, Camera camera)
        {
            _session = session;
            _camera = camera;
        }

        private void Update()
        {
            var self = _session?.Connection.SelfSnapshot();
            if (self == null || _session.Dead)
            {
                _session?.Connection.SendInput(0, 0);
                return;
            }

            var position = new Vector2(self.Value.X, self.Value.Y);
            ReadClick();
            var direction = KeyboardDirection();
            if (direction != Vector2.zero)
            {
                _moveTo = null;
                _target = null;
                _session.Menu = null;
            }
            else if (_target != null)
            {
                direction = DirectionToTarget(position);
            }
            else if (_moveTo.HasValue)
            {
                direction = DirectionToPoint(position, _moveTo.Value, 0.15f);
                if (direction == Vector2.zero)
                {
                    _moveTo = null;
                }
            }

            SendDirection(direction);
            StopIfStuck(position, direction);
            CloseMenuIfWalkedAway(position);
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
            float distance;
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out distance))
            {
                return;
            }

            double x, y;
            WorldCoords.FromUnity(ray.GetPoint(distance), out x, out y);
            _session.Menu = null;
            _stuckTime = 0;
            _target = PickTarget(x, y);
            _moveTo = _target == null ? new Vector2((float)x, (float)y) : (Vector2?)null;
        }

        private InteractionTarget PickTarget(double x, double y)
        {
            var world = _session.Connection.World;
            var visuals = _session.Connection.PlantVisuals;
            var best = PickRadius;
            var found = -1;
            for (var i = 0; i < world.Plants.Length; i++)
            {
                var plant = world.Plants[i];
                var d = Distance(plant.X, plant.Y, x, y);
                if (d < best && visuals[i] != PlantVisual.Gone)
                {
                    best = d;
                    found = i;
                }
            }

            if (found >= 0)
            {
                return InteractionTarget.ForPlant(found, world.Plants[found].X, world.Plants[found].Y);
            }

            var tx = (int)System.Math.Floor(x);
            var ty = (int)System.Math.Floor(y);
            if (tx >= 0 && ty >= 0 && tx < world.Width && ty < world.Height && IsWater(world.TileAt(tx, ty)))
            {
                return InteractionTarget.ForWater(tx, ty);
            }

            return null;
        }

        /// <summary>WASD a šipky. „Nahoru“ je směr, kam se dívá kamera. Výsledek je směr na mapě (y dolů).</summary>
        private Vector2 KeyboardDirection()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            var input = Vector2.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1;
            if (input == Vector2.zero)
            {
                return Vector2.zero;
            }

            var forward = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(_camera.transform.right, Vector3.up).normalized;
            var unity = (forward * input.y + right * input.x).normalized;
            return new Vector2(unity.x, -unity.z);
        }

        /// <summary>Jde k cíli. Když je dost blízko, otevře nabídku (jako v prototypu).</summary>
        private Vector2 DirectionToTarget(Vector2 position)
        {
            var reach = _target.IsPlant ? PlantReach : WaterReach;
            var target = new Vector2((float)_target.X, (float)_target.Y);
            if (Vector2.Distance(position, target) <= reach - 0.15)
            {
                _session.Menu = _target;
                _target = null;
                return Vector2.zero;
            }

            return (target - position).normalized;
        }

        private static Vector2 DirectionToPoint(Vector2 position, Vector2 point, float stopDistance)
        {
            var offset = point - position;
            return offset.magnitude < stopDistance ? Vector2.zero : offset.normalized;
        }

        /// <summary>Pošle směr serveru jen při znatelné změně, aby se zbytečně nezahlcovala síť.</summary>
        private void SendDirection(Vector2 direction)
        {
            if (Vector2.Distance(direction, _sent) < 0.05f && (direction == Vector2.zero) == (_sent == Vector2.zero))
            {
                return;
            }

            _sent = direction;
            _session.Connection.SendInput(direction.x, direction.y);
        }

        /// <summary>Když postava při chůzi na kliknuté místo nebo k cíli narazí na překážku, po chvíli to vzdá.</summary>
        private void StopIfStuck(Vector2 position, Vector2 direction)
        {
            var walkingToPoint = _moveTo.HasValue || _target != null;
            if (!walkingToPoint || direction == Vector2.zero)
            {
                _stuckTime = 0;
                _lastPosition = position;
                return;
            }

            _stuckTime = Vector2.Distance(position, _lastPosition) < 0.01f ? _stuckTime + Time.deltaTime : 0;
            _lastPosition = position;
            if (_stuckTime > StuckSeconds)
            {
                _moveTo = null;
                _target = null;
                _stuckTime = 0;
            }
        }

        private void CloseMenuIfWalkedAway(Vector2 position)
        {
            var menu = _session.Menu;
            if (menu == null)
            {
                return;
            }

            var reach = menu.IsPlant ? PlantReach : WaterReach;
            if (Vector2.Distance(position, new Vector2((float)menu.X, (float)menu.Y)) > reach + MenuCloseExtra)
            {
                _session.Menu = null;
            }
        }

        private static bool IsWater(TileType tile)
        {
            return tile == TileType.Pond || tile == TileType.Bog || tile == TileType.Sea;
        }

        private static double Distance(double ax, double ay, double bx, double by)
        {
            var dx = ax - bx;
            var dy = ay - by;
            return System.Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
