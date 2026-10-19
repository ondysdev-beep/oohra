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
        public BuildOption(BuildKind kind, string name, string cost, int sticks, int stones, float length = StructureViews.WallLength)
        {
            Kind = kind;
            Name = name;
            Cost = cost;
            Sticks = sticks;
            Stones = stones;
            Length = length;
        }

        public BuildKind Kind { get; }

        public string Name { get; }

        public string Cost { get; }

        public int Sticks { get; }

        public int Stones { get; }

        /// <summary>Délka zdi v metrech (poloviční zeď 0,5).</summary>
        public float Length { get; }

        /// <summary>Dekorace: id z decorations.json na serveru, jinak prázdné.</summary>
        public string DecorationId { get; private set; } = string.Empty;

        /// <summary>Dekorace: poloměr v metrech (pro náhled bez modelu).</summary>
        public float Radius { get; private set; } = 0.3f;

        /// <summary>Položka nabídky pro dekoraci ze serveru.</summary>
        public static BuildOption ForDecoration(DecorationTypeView decoration)
        {
            return new BuildOption(BuildKind.Decoration, decoration.Name, CostText(decoration.Sticks, decoration.Stones), decoration.Sticks, decoration.Stones)
            {
                DecorationId = decoration.Id,
                Radius = decoration.Radius,
            };
        }

        /// <summary>Cena česky: „3 klacky a kámen“, „4 kameny“.</summary>
        public static string CostText(int sticks, int stones)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (sticks > 0) parts.Add(sticks == 1 ? "klacek" : sticks + (sticks < 5 ? " klacky" : " klacků"));
            if (stones > 0) parts.Add(stones == 1 ? "kámen" : stones + (stones < 5 ? " kameny" : " kamenů"));
            return parts.Count == 0 ? "zdarma" : string.Join(" a ", parts);
        }
    }

    /// <summary>
    /// Režim stavby pro všechny stavby. G ho zapne a vypne, F ho otevře rovnou s ohněm, 1 až 9 vybírá stavbu (za stavbami z kódu jsou dekorace ze serveru).
    /// U kurzoru je průsvitný náhled stavby, nejdál 3,5 m od postavy. Levé tlačítko staví, pravé tlačítko
    /// nebo Esc režim ukončí. R přepíná volnou a modulární stavbu:
    /// volně Q a E stavbou plynule otáčejí; modulárně se zeď přichytí ke konci jiné zdi (směr určí kurzor,
    /// Q a E ji kolem toho konce natáčejí po 15°) a v mezeře mezi dvěma konci se natáhne tak, aby zapadla.
    /// Dveře se vždy vsadí do nejbližší zdi s otvorem. Jestli se stavba opravdu postaví, rozhodne server.
    /// </summary>
    public class BuildMode : MonoBehaviour
    {
        // Jak daleko smí postava stavět (jako placeReach v building.json, server to stejně ověří).
        public const float Reach = 3.5f;

        // Jak blízko u otvoru musí kurzor být, aby se do něj vsadily dveře (jako snapDistance v building.json).
        private const float DoorSnap = 0.7f;

        private const float RotateSpeed = 90f;
        private const float ModularStep = 15f;
        private static readonly Color CanBuild = new Color(0.45f, 0.85f, 0.45f, 0.45f);
        private static readonly Color CannotBuild = new Color(0.9f, 0.35f, 0.3f, 0.45f);

        public static readonly BuildOption[] Catalog =
        {
            new BuildOption(BuildKind.StickWall, "Zeď z klacků", "2 klacky", 2, 0),
            new BuildOption(BuildKind.StickWall, "Poloviční zeď", "1 klacek", 1, 0, 0.5f),
            new BuildOption(BuildKind.DoorFrame, "Zeď s otvorem na dveře", "2 klacky", 2, 0),
            new BuildOption(BuildKind.Door, "Dveře (do otvoru)", "2 klacky", 2, 0),
            new BuildOption(BuildKind.Campfire, "Oheň", "3 klacky a kámen", 3, 1),
            new BuildOption(BuildKind.Trap, "Past (chytí králíka nebo jelena)", "3 klacky", 3, 0),
            new BuildOption(BuildKind.Stockpile, "Zásoby kmene (koš)", "4 klacky", 4, 0),
            new BuildOption(BuildKind.Barricade, "Zátaras u vchodu do jeskyně", "6 klacků", 6, 0),
        };

        private ClientSession _session;
        private Camera _camera;
        private DecorationTypeView[] _decorationSource;
        private BuildOption[] _options = Catalog;
        private GameObject _ghost;
        private bool _placeable;

        /// <summary>Režim stavby je zapnutý (kliknutí pak nestaví postavu do pohybu).</summary>
        public static bool IsActive { get; private set; }

        public BuildOption SelectedOption { get; private set; } = Catalog[0];

        public BuildKind Selected
        {
            get { return SelectedOption.Kind; }
        }

        /// <summary>Modulární stavba: zdi se přichytávají ke koncům jiných zdí a dorovnávají mezery.</summary>
        public bool Modular { get; set; }

        /// <summary>Úhel stavby ve stupních (0 = zeď vede od západu na východ). Modulárně pootočení kolem konce zdi.</summary>
        public float AngleDegrees { get; set; }

        /// <summary>Kde by stavba stála (střed na mapě), pod jakým úhlem a jak dlouhá, po přichycení.</summary>
        public Vector2 Target { get; private set; }

        public float TargetAngleDegrees { get; private set; }

        public float TargetLength { get; private set; } = StructureViews.WallLength;

        public void Init(ClientSession session, Camera camera)
        {
            _session = session;
            _camera = camera;
        }

        /// <summary>
        /// Celá nabídka: stavby z kódu (Catalog) a za nimi dekorace, které poslal server (decorations.json).
        /// </summary>
        public BuildOption[] Options
        {
            get
            {
                var decorations = _session != null ? _session.Connection.DecorationTypes : null;
                if (decorations != null && !ReferenceEquals(decorations, _decorationSource))
                {
                    _decorationSource = decorations;
                    var options = new System.Collections.Generic.List<BuildOption>(Catalog);
                    foreach (var decoration in decorations)
                    {
                        options.Add(BuildOption.ForDecoration(decoration));
                    }

                    _options = options.ToArray();
                }

                return _options;
            }
        }

        /// <summary>Otevře režim stavby s první položkou daného druhu.</summary>
        public void Open(BuildKind kind)
        {
            foreach (var option in Options)
            {
                if (option.Kind == kind)
                {
                    Open(option);
                    return;
                }
            }
        }

        public void Open(BuildOption option)
        {
            IsActive = true;
            CameraRig.KeysTurnCamera = false;
            _session.Menu = null;
            _session.Selected = null;
            _session.BackpackOpen = false;
            if (_ghost == null || SelectedOption != option)
            {
                SelectedOption = option;
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
            Send();
        }

        /// <summary>Má na vybranou stavbu materiál (jen pro barvu náhledu, rozhodne server).</summary>
        public bool HasMaterials()
        {
            var sticks = Selected == BuildKind.StickWall ? (TargetLength <= 0.5f + 0.001f ? 1 : 2) : SelectedOption.Sticks;
            return _session.Count(ItemKind.Stick) >= sticks && _session.Count(ItemKind.Stone) >= SelectedOption.Stones;
        }

        private void Send()
        {
            _session.Place(Selected, Target.x, Target.y, TargetAngleDegrees * Mathf.Deg2Rad, TargetLength, SelectedOption.DecorationId);
        }

        private void Update()
        {
            // Během snu bez slov (nováček) se rozhraní neukazuje a hra nereaguje na ovládání.
            if (DreamView.Active)
            {
                return;
            }

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
                Send();
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

            if (keyboard.gKey.wasPressedThisFrame)
            {
                if (IsActive) Close();
                else Open(SelectedOption.Kind == BuildKind.Campfire ? Catalog[0] : SelectedOption);
            }

            if (keyboard.fKey.wasPressedThisFrame)
            {
                if (IsActive && Selected == BuildKind.Campfire) Close();
                else Open(BuildKind.Campfire);
            }

            if (!IsActive)
            {
                return;
            }

            var digits = new[] { keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key, keyboard.digit4Key, keyboard.digit5Key, keyboard.digit6Key, keyboard.digit7Key, keyboard.digit8Key, keyboard.digit9Key };
            for (var i = 0; i < Options.Length && i < digits.Length; i++)
            {
                if (digits[i].wasPressedThisFrame && !keyboard.shiftKey.isPressed) Open(Options[i]);
            }

            if (keyboard.rKey.wasPressedThisFrame) Modular = !Modular;
            if (keyboard.escapeKey.wasPressedThisFrame) Close();

            if (Modular)
            {
                if (keyboard.qKey.wasPressedThisFrame) AngleDegrees -= ModularStep;
                if (keyboard.eKey.wasPressedThisFrame) AngleDegrees += ModularStep;
            }
            else
            {
                if (keyboard.qKey.isPressed) AngleDegrees -= RotateSpeed * Time.deltaTime;
                if (keyboard.eKey.isPressed) AngleDegrees += RotateSpeed * Time.deltaTime;
            }
        }

        /// <summary>Spočítá, kde, pod jakým úhlem a jak dlouhá by stavba byla, když kurzor míří na místo pointer.</summary>
        private void Aim(Vector2 pointer)
        {
            _placeable = true;
            TargetLength = SelectedOption.Length;
            if (Selected == BuildKind.Door)
            {
                AimDoor(pointer);
                return;
            }

            var isWall = Selected == BuildKind.StickWall || Selected == BuildKind.DoorFrame;
            SnapResult snap;
            if (Modular && isWall && ModularSnap.Find(_session.Connection.Structures, pointer, SelectedOption.Length, AngleDegrees, Selected == BuildKind.StickWall, out snap))
            {
                Target = snap.Center;
                TargetAngleDegrees = snap.AngleDegrees;
                TargetLength = snap.Length;
                _placeable = InReach(Target);
                return;
            }

            TargetAngleDegrees = AngleDegrees;
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

            _ghost = StructureViews.CreateModel(Selected, null, SelectedOption.Length, SelectedOption.DecorationId, SelectedOption.Radius);
            _ghost.name = "Náhled stavby";
        }

        private void ShowGhost()
        {
            StructureViews.PlaceModel(_ghost.transform, Target.x, Target.y, TargetAngleDegrees * Mathf.Deg2Rad);
            if (Selected == BuildKind.StickWall)
            {
                // Délka zdi se při dorovnávání mění, náhled se natáhne.
                StructureViews.StretchWall(_ghost, TargetLength);
            }

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
