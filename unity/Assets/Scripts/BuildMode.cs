using Oohra.Core.Items;
using Oohra.Core.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Oohra.Client
{
    /// <summary>Položka v nabídce režimu stavby.</summary>
    public sealed class BuildOption
    {
        public BuildOption(BuildKind kind, string name, string cost)
        {
            Kind = kind;
            Name = name;
            Cost = cost;
        }

        public BuildKind Kind { get; }

        public string Name { get; }

        public string Cost { get; }
    }

    /// <summary>
    /// Režim stavby pro všechny stavby (zatím zeď z klacků a oheň). G ho zapne a vypne, F ho otevře rovnou s ohněm.
    /// U kurzoru je průsvitný náhled stavby, nejdál 1,2 m od postavy. Q a E stavbou otáčejí (kamerou v tu chvíli ne),
    /// levé tlačítko myši staví, pravé tlačítko nebo Esc režim ukončí. Jestli se stavba opravdu postaví, rozhodne server.
    /// </summary>
    public class BuildMode : MonoBehaviour
    {
        // Jak daleko smí postava stavět (jako placeReach v building.json, server to stejně ověří).
        public const float Reach = 1.2f;
        private const float RotateSpeed = 90f;
        private static readonly Color CanBuild = new Color(0.45f, 0.85f, 0.45f, 0.45f);
        private static readonly Color CannotBuild = new Color(0.9f, 0.35f, 0.3f, 0.45f);

        public static readonly BuildOption[] Catalog =
        {
            new BuildOption(BuildKind.StickWall, "Zeď z klacků", "2 klacky"),
            new BuildOption(BuildKind.Campfire, "Oheň", "3 klacky a kámen"),
        };

        private ClientSession _session;
        private Camera _camera;
        private GameObject _ghost;

        /// <summary>Režim stavby je zapnutý (kliknutí pak nestaví postavu do pohybu).</summary>
        public static bool IsActive { get; private set; }

        public BuildKind Selected { get; private set; }

        /// <summary>Úhel stavby ve stupních (0 = zeď vede od západu na východ).</summary>
        public float AngleDegrees { get; set; }

        /// <summary>Kde by stavba stála (střed na mapě), už omezené na dosah.</summary>
        public Vector2 Target { get; private set; }

        public void Init(ClientSession session, Camera camera)
        {
            _session = session;
            _camera = camera;
        }

        public void Open(BuildKind kind)
        {
            IsActive = true;
            CameraRig.KeysTurnCamera = false;
            _session.Menu = null;
            _session.BackpackOpen = false;
            if (_ghost == null || Selected != kind)
            {
                Selected = kind;
                CreateGhost();
            }
        }

        public void Close()
        {
            IsActive = false;
            CameraRig.KeysTurnCamera = true;
            if (_ghost != null)
            {
                Destroy(_ghost);
                _ghost = null;
            }
        }

        /// <summary>Postaví vybranou stavbu se středem (x, y) na mapě. Když je to dál než Reach, posune ji blíž k postavě.</summary>
        public void PlaceAt(double x, double y)
        {
            var target = ClampToReach(new Vector2((float)x, (float)y));
            _session.Place(Selected, target.x, target.y, AngleDegrees * Mathf.Deg2Rad);
        }

        /// <summary>Má na vybranou stavbu materiál (jen pro barvu náhledu, rozhodne server).</summary>
        public bool HasMaterials()
        {
            var sticks = _session.Count(ItemKind.Stick);
            return Selected == BuildKind.StickWall ? sticks >= 2 : sticks >= 3 && _session.Count(ItemKind.Stone) >= 1;
        }

        private void Update()
        {
            if (_session == null)
            {
                return;
            }

            ReadKeys();
            if (!IsActive)
            {
                return;
            }

            if (_session.Dead)
            {
                Close();
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.qKey.isPressed) AngleDegrees -= RotateSpeed * Time.deltaTime;
                if (keyboard.eKey.isPressed) AngleDegrees += RotateSpeed * Time.deltaTime;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var pointer = PointerOnMap(mouse.position.ReadValue());
            if (pointer.HasValue)
            {
                Target = ClampToReach(pointer.Value);
                ShowGhost();
            }

            if (mouse.leftButton.wasPressedThisFrame && !GameHud.MouseOverHud && pointer.HasValue)
            {
                PlaceAt(Target.x, Target.y);
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                Close();
            }
        }

        private void ReadKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || _session.Dead)
            {
                return;
            }

            if (keyboard.gKey.wasPressedThisFrame) Toggle(BuildKind.StickWall);
            if (keyboard.fKey.wasPressedThisFrame) Toggle(BuildKind.Campfire);
            if (!IsActive)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame) Open(Catalog[0].Kind);
            if (keyboard.digit2Key.wasPressedThisFrame) Open(Catalog[1].Kind);
            if (keyboard.escapeKey.wasPressedThisFrame) Close();
        }

        /// <summary>Stejná klávesa režim zavře, jiná přepne stavbu.</summary>
        private void Toggle(BuildKind kind)
        {
            if (IsActive && Selected == kind)
            {
                Close();
            }
            else
            {
                Open(kind);
            }
        }

        private Vector2? PointerOnMap(Vector2 screen)
        {
            var ray = _camera.ScreenPointToRay(screen);
            float distance;
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out distance))
            {
                return null;
            }

            double x, y;
            WorldCoords.FromUnity(ray.GetPoint(distance), out x, out y);
            return new Vector2((float)x, (float)y);
        }

        private Vector2 ClampToReach(Vector2 point)
        {
            var self = _session.Connection.SelfSnapshot();
            if (self == null)
            {
                return point;
            }

            var player = new Vector2(self.Value.X, self.Value.Y);
            var offset = point - player;
            // Malá rezerva: na hranici by zaokrouhlení (float na klientu, double na serveru) dalo „moc daleko“.
            var reach = Reach - 0.02f;
            return offset.magnitude <= reach ? point : player + offset.normalized * reach;
        }

        private void CreateGhost()
        {
            if (_ghost != null)
            {
                Destroy(_ghost);
            }

            var shape = Selected == BuildKind.StickWall ? PrimitiveType.Cube : PrimitiveType.Cylinder;
            _ghost = IslandView.Primitive(shape, "Náhled stavby", null, CanBuild);
        }

        private void ShowGhost()
        {
            if (Selected == BuildKind.StickWall)
            {
                StructureViews.PlaceWall(_ghost.transform, Target.x, Target.y, AngleDegrees * Mathf.Deg2Rad);
            }
            else
            {
                _ghost.transform.position = WorldCoords.ToUnity(Target.x, Target.y, 0.1f);
                _ghost.transform.localScale = new Vector3(0.7f, 0.1f, 0.7f);
            }

            Palette.SetFade(_ghost, HasMaterials() ? CanBuild : CannotBuild);
        }

        private void OnDestroy()
        {
            IsActive = false;
            CameraRig.KeysTurnCamera = true;
        }
    }
}
