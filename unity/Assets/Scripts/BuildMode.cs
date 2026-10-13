using Oohra.Core.Items;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Oohra.Client
{
    /// <summary>Položka v nabídce režimu stavby.</summary>
    public sealed class BuildOption
    {
        public BuildOption(BuildKind kind, string name, string cost, int sticks, int stones)
        {
            Kind = kind;
            Name = name;
            Cost = cost;
            Sticks = sticks;
            Stones = stones;
        }

        public BuildKind Kind { get; }

        public string Name { get; }

        public string Cost { get; }

        public int Sticks { get; }

        public int Stones { get; }
    }

    /// <summary>
    /// Režim stavby pro všechny stavby. G ho zapne a vypne, F ho otevře rovnou s ohněm, 1 až 4 vybírá stavbu.
    /// U kurzoru je průsvitný náhled stavby, nejdál 3,5 m od postavy. Levé tlačítko staví, pravé tlačítko
    /// nebo Esc režim ukončí. R přepíná volnou a modulární stavbu:
    /// volně Q a E stavbou plynule otáčejí; modulárně se zeď přichytí ke konci jiné zdi (vede tam, kam míří
    /// kurzor) a Q a E otáčejí po 90°. Dveře se vždy vsadí do nejbližší zdi s otvorem.
    /// Jestli se stavba opravdu postaví, rozhodne server.
    /// </summary>
    public class BuildMode : MonoBehaviour
    {
        // Jak daleko smí postava stavět (jako placeReach v building.json, server to stejně ověří).
        public const float Reach = 3.5f;

        // Jak blízko u konce jiné zdi musí kurzor být, aby se nová zeď přichytila (modulárně).
        private const float SnapRadius = 0.8f;

        // Jak blízko u otvoru musí kurzor být, aby se do něj vsadily dveře (jako snapDistance v building.json).
        private const float DoorSnap = 0.7f;

        private const float RotateSpeed = 90f;
        private static readonly Color CanBuild = new Color(0.45f, 0.85f, 0.45f, 0.45f);
        private static readonly Color CannotBuild = new Color(0.9f, 0.35f, 0.3f, 0.45f);

        public static readonly BuildOption[] Catalog =
        {
            new BuildOption(BuildKind.StickWall, "Zeď z klacků", "2 klacky", 2, 0),
            new BuildOption(BuildKind.DoorFrame, "Zeď s otvorem na dveře", "2 klacky", 2, 0),
            new BuildOption(BuildKind.Door, "Dveře (do otvoru)", "2 klacky", 2, 0),
            new BuildOption(BuildKind.Campfire, "Oheň", "3 klacky a kámen", 3, 1),
        };

        private ClientSession _session;
        private Camera _camera;
        private GameObject _ghost;
        private bool _placeable;

        /// <summary>Režim stavby je zapnutý (kliknutí pak nestaví postavu do pohybu).</summary>
        public static bool IsActive { get; private set; }

        public BuildKind Selected { get; private set; }

        /// <summary>Modulární stavba: zdi se přichytávají ke koncům jiných zdí a otáčejí se po 90°.</summary>
        public bool Modular { get; set; }

        /// <summary>Úhel stavby ve stupních (0 = zeď vede od západu na východ).</summary>
        public float AngleDegrees { get; set; }

        /// <summary>Kde by stavba stála (střed na mapě) a pod jakým úhlem, po přichycení a omezení na dosah.</summary>
        public Vector2 Target { get; private set; }

        public float TargetAngleDegrees { get; private set; }

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
            _session.Selected = null;
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

        /// <summary>
        /// Postaví vybranou stavbu s kurzorem na místě (x, y) na mapě: přichytí ji (modulárně, dveře do otvoru)
        /// a omezí na dosah. Totéž dělá levé tlačítko myši.
        /// </summary>
        public void PlaceAt(double x, double y)
        {
            Aim(new Vector2((float)x, (float)y));
            _session.Place(Selected, Target.x, Target.y, TargetAngleDegrees * Mathf.Deg2Rad);
        }

        /// <summary>Má na vybranou stavbu materiál (jen pro barvu náhledu, rozhodne server).</summary>
        public bool HasMaterials()
        {
            var option = OptionFor(Selected);
            return _session.Count(ItemKind.Stick) >= option.Sticks && _session.Count(ItemKind.Stone) >= option.Stones;
        }

        public static BuildOption OptionFor(BuildKind kind)
        {
            foreach (var option in Catalog)
            {
                if (option.Kind == kind)
                {
                    return option;
                }
            }

            return Catalog[0];
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

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var pointer = PointerOnMap(mouse.position.ReadValue());
            if (pointer.HasValue)
            {
                Aim(pointer.Value);
                ShowGhost();
            }

            if (mouse.leftButton.wasPressedThisFrame && !GameHud.MouseOverHud && pointer.HasValue)
            {
                _session.Place(Selected, Target.x, Target.y, TargetAngleDegrees * Mathf.Deg2Rad);
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

            if (keyboard.gKey.wasPressedThisFrame) Toggle(IsActive ? Selected : BuildKind.StickWall);
            if (keyboard.fKey.wasPressedThisFrame) Toggle(BuildKind.Campfire);
            if (!IsActive)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame) Open(Catalog[0].Kind);
            if (keyboard.digit2Key.wasPressedThisFrame) Open(Catalog[1].Kind);
            if (keyboard.digit3Key.wasPressedThisFrame) Open(Catalog[2].Kind);
            if (keyboard.digit4Key.wasPressedThisFrame) Open(Catalog[3].Kind);
            if (keyboard.rKey.wasPressedThisFrame) Modular = !Modular;
            if (keyboard.escapeKey.wasPressedThisFrame) Close();

            if (Modular)
            {
                if (keyboard.qKey.wasPressedThisFrame) AngleDegrees = Mathf.Round(AngleDegrees / 90f) * 90f - 90f;
                if (keyboard.eKey.wasPressedThisFrame) AngleDegrees = Mathf.Round(AngleDegrees / 90f) * 90f + 90f;
            }
            else
            {
                if (keyboard.qKey.isPressed) AngleDegrees -= RotateSpeed * Time.deltaTime;
                if (keyboard.eKey.isPressed) AngleDegrees += RotateSpeed * Time.deltaTime;
            }
        }

        /// <summary>F a G: stejná stavba režim zavře, jiná přepne.</summary>
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

        /// <summary>Spočítá, kde a pod jakým úhlem by stavba stála, když kurzor míří na místo pointer.</summary>
        private void Aim(Vector2 pointer)
        {
            _placeable = true;
            if (Selected == BuildKind.Door)
            {
                AimDoor(pointer);
                return;
            }

            var isWall = Selected == BuildKind.StickWall || Selected == BuildKind.DoorFrame;
            if (Modular && isWall && SnapToWallEnd(pointer))
            {
                return;
            }

            TargetAngleDegrees = Modular ? Mathf.Round(AngleDegrees / 90f) * 90f : AngleDegrees;
            Target = ClampToReach(pointer);
        }

        /// <summary>Dveře: do nejbližší zdi s otvorem bez dveří. Když žádná není, náhled je červený.</summary>
        private void AimDoor(Vector2 pointer)
        {
            var best = DoorSnap;
            StructureView? frame = null;
            foreach (var structure in _session.Connection.Structures)
            {
                var distance = Vector2.Distance(pointer, new Vector2(structure.X, structure.Y));
                if (structure.Kind == BuildKind.DoorFrame && distance < best && !HasDoor(structure))
                {
                    best = distance;
                    frame = structure;
                }
            }

            if (frame.HasValue)
            {
                Target = new Vector2(frame.Value.X, frame.Value.Y);
                TargetAngleDegrees = frame.Value.Angle * Mathf.Rad2Deg;
                _placeable = InReach(Target);
            }
            else
            {
                Target = ClampToReach(pointer);
                TargetAngleDegrees = AngleDegrees;
                _placeable = false;
            }
        }

        /// <summary>V otvoru už jsou dveře (dveře mají stejný střed jako jejich zeď s otvorem).</summary>
        private bool HasDoor(StructureView frame)
        {
            foreach (var structure in _session.Connection.Structures)
            {
                if (structure.Kind == BuildKind.Door && Mathf.Abs(structure.X - frame.X) < 0.01f && Mathf.Abs(structure.Y - frame.Y) < 0.01f)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Modulárně: najde konec jiné zdi blízko kurzoru a postaví novou zeď tak, aby z něj vycházela.
        /// Ze čtyř směrů (rovně a do stran po 90°) vybere ten, kam míří kurzor. Zpátky do staré zdi ne.
        /// </summary>
        private bool SnapToWallEnd(Vector2 pointer)
        {
            var bestDistance = SnapRadius;
            var found = false;
            var end = Vector2.zero;
            var wallCenter = Vector2.zero;
            var wallAngle = 0f;
            foreach (var structure in _session.Connection.Structures)
            {
                if (structure.Kind != BuildKind.StickWall && structure.Kind != BuildKind.DoorFrame)
                {
                    continue;
                }

                var center = new Vector2(structure.X, structure.Y);
                var half = new Vector2(Mathf.Cos(structure.Angle), Mathf.Sin(structure.Angle)) * (StructureViews.WallLength / 2);
                foreach (var candidate in new[] { center - half, center + half })
                {
                    var distance = Vector2.Distance(pointer, candidate);
                    if (distance < bestDistance && InReach(candidate))
                    {
                        bestDistance = distance;
                        found = true;
                        end = candidate;
                        wallCenter = center;
                        wallAngle = structure.Angle * Mathf.Rad2Deg;
                    }
                }
            }

            if (!found)
            {
                return false;
            }

            var bestScore = float.MaxValue;
            for (var k = 0; k < 4; k++)
            {
                var angle = wallAngle + k * 90f;
                var direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var center = end + direction * (StructureViews.WallLength / 2);
                if (Vector2.Distance(center, wallCenter) < StructureViews.WallLength * 0.6f)
                {
                    continue;
                }

                var score = Vector2.Distance(center, pointer);
                if (score < bestScore)
                {
                    bestScore = score;
                    Target = center;
                    TargetAngleDegrees = angle;
                }
            }

            _placeable = InReach(Target);
            return true;
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

        private Vector2 Player()
        {
            var self = _session.Connection.SelfSnapshot();
            return self == null ? Vector2.zero : new Vector2(self.Value.X, self.Value.Y);
        }

        private bool InReach(Vector2 point)
        {
            // Malá rezerva: na hranici by zaokrouhlení (float na klientu, double na serveru) dalo „moc daleko“.
            return Vector2.Distance(point, Player()) <= Reach - 0.02f;
        }

        private Vector2 ClampToReach(Vector2 point)
        {
            var player = Player();
            var offset = point - player;
            var reach = Reach - 0.02f;
            return offset.magnitude <= reach ? point : player + offset.normalized * reach;
        }

        private void CreateGhost()
        {
            if (_ghost != null)
            {
                Destroy(_ghost);
            }

            _ghost = StructureViews.CreateModel(Selected, null);
            _ghost.name = "Náhled stavby";
        }

        private void ShowGhost()
        {
            StructureViews.PlaceModel(_ghost.transform, Target.x, Target.y, TargetAngleDegrees * Mathf.Deg2Rad);
            var color = _placeable && HasMaterials() ? CanBuild : CannotBuild;
            foreach (var renderer in _ghost.GetComponentsInChildren<Renderer>())
            {
                Palette.SetFade(renderer.gameObject, color);
            }
        }

        private void OnDestroy()
        {
            IsActive = false;
            CameraRig.KeysTurnCamera = true;
        }
    }
}
