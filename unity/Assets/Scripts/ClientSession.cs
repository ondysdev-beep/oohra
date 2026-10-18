using System.Collections.Generic;
using System.Linq;
using Oohra.Core.Items;
using Oohra.Core.Language;
using Oohra.Core.Plants;
using Oohra.Core.Social;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>Co hráč chce nahlásit: hráče, značku nebo obecný problém (TargetId se pak nepoužije).</summary>
    public sealed class ReportRequest
    {
        public ReportRequest(ReportTarget target, int targetId, string what)
        {
            Target = target;
            TargetId = targetId;
            What = what;
        }

        public ReportTarget Target { get; }

        public int TargetId { get; }

        /// <summary>Co se nahlašuje, do nadpisu panelu (přezdívka, „značku“, „problém“).</summary>
        public string What { get; }
    }

    /// <summary>Řádek v seznamu hlášek. Warn = špatná zpráva (zvýrazní se).</summary>
    public sealed class LogLine
    {
        public LogLine(string text, bool warn, float shownAt)
        {
            Text = text;
            Warn = warn;
            ShownAt = shownAt;
        }

        public string Text { get; }

        public bool Warn { get; }

        public float ShownAt { get; }
    }

    /// <summary>
    /// Hra na straně klienta: spojení se serverem, čas, otevřená nabídka a hlášky.
    /// Klient jen posílá úmysly (utrhni, napij se, sněz). Co se stane, rozhodne server a pošle výsledek.
    /// </summary>
    public sealed class ClientSession
    {
        private const int MaxLogLines = 6;

        // Dosah rány (jako hunt.reach v animals.json, server ho stejně ověří).
        private const float AttackReach = 1.6f;

        private readonly List<LogLine> _log = new List<LogLine>();

        public ClientSession(GameConnection connection)
        {
            Connection = connection;
        }

        public GameConnection Connection { get; }

        public ClientClock Clock { get; } = new ClientClock();

        public IReadOnlyList<LogLine> Log => _log;

        /// <summary>Každá hláška ze serveru (nápovědy a oslavy „poprvé“ podle ní poznají, co se stalo).</summary>
        public event System.Action<GameEvent> EventReceived;

        public bool BackpackOpen { get; set; }

        /// <summary>Panel dovedností (klávesa K).</summary>
        public bool SkillsOpen { get; set; }

        public bool Sleeping => Connection.Self != null && Connection.Self.Sleeping;

        /// <summary>Nabídka u rostliny nebo vody, když k ní postava došla.</summary>
        public InteractionTarget Menu { get; set; }

        /// <summary>Vybraná věc (pod ní je kruh): na co hráč kliknul, dokud nejde jinam.</summary>
        public InteractionTarget Selected { get; set; }

        public bool Dead => Connection.Self != null && Connection.Self.Dead;

        /// <summary>Co hráč chce nahlásit (panel Nahlásit ukazuje důvody), nebo null.</summary>
        public ReportRequest PendingReport { get; set; }

        /// <summary>Otevře nahlášení hráče, značky nebo obecného problému. what je, co se ukáže v nadpisu.</summary>
        public void AskReport(ReportTarget target, int targetId, string what)
        {
            PendingReport = new ReportRequest(target, targetId, what);
        }

        /// <summary>Pošle nahlášení s důvodem a zavře panel. Výsledek přijde jako hláška.</summary>
        public void SendReport(ReportReason reason)
        {
            if (PendingReport == null)
            {
                return;
            }

            Connection.Report(PendingReport.Target, PendingReport.TargetId, reason);
            PendingReport = null;
        }

        /// <summary>Zpracuje zprávy od serveru. Volat každý snímek.</summary>
        public void Update()
        {
            Connection.Poll();
            var snapshot = Connection.Snapshot;
            if (snapshot != null)
            {
                Clock.TotalMinutes = snapshot.TotalMinutes;
                Clock.CalendarMonths = snapshot.CalendarMonths;
            }

            foreach (var gameEvent in Connection.TakeEvents())
            {
                EventReceived?.Invoke(gameEvent);
                bool warn;
                var text = Texts.For(gameEvent, out warn);
                if (text != null)
                {
                    Say(text, warn);
                }
            }

            foreach (var announcement in Connection.TakeAnnouncements())
            {
                Say("Oznámení správce: " + announcement, true);
            }
        }

        public void Harvest(PlantPart part)
        {
            if (Menu != null && Menu.IsPlant)
            {
                Connection.Harvest(Menu.PlantId, part);
                if (part == PlantPart.Root)
                {
                    Menu = null;
                    Selected = null;
                }
            }
        }

        public void Drink()
        {
            if (Menu != null && Menu.Kind == TargetKind.Water)
            {
                Connection.Drink(Menu.TileX, Menu.TileY);
            }
        }

        /// <summary>U stromu nasbírat klacky, u skály vzít kámen.</summary>
        public void GatherMaterial()
        {
            if (Menu == null)
            {
                return;
            }

            if (Menu.Kind == TargetKind.Tree) Connection.GatherSticks(Menu.TileX, Menu.TileY);
            if (Menu.Kind == TargetKind.Rock) Connection.TakeStone(Menu.TileX, Menu.TileY);
        }

        /// <summary>Přiložit klacek do ohně, u kterého je otevřená nabídka (vyhaslý znovu zapálit).</summary>
        public void FeedFire()
        {
            if (Menu != null && Menu.Kind == TargetKind.Fire)
            {
                Connection.FeedFire(Menu.Id);
            }
        }

        /// <summary>Zapálit pochodeň od ohně (klacek z batohu). Musí být u hořícího ohně, rozhodne server.</summary>
        public void LightTorch()
        {
            Connection.LightTorch();
        }

        /// <summary>Drží vlastní postava zapálenou pochodeň?</summary>
        public bool TorchLit
        {
            get
            {
                var self = Connection.SelfSnapshot();
                return self.HasValue && self.Value.Torch;
            }
        }

        /// <summary>Režim stavby: postavit stavbu se středem (x, y) a úhlem v radiánech. Rozhodne server.</summary>
        public void Place(BuildKind kind, double x, double y, float angle, float length = 1f, string decoration = "")
        {
            Connection.Place(kind, (float)x, (float)y, angle, length, decoration);
        }

        /// <summary>Rozebrat stavbu, u které je otevřená nabídka.</summary>
        public void RemoveStructure()
        {
            if (Menu != null && Menu.Kind == TargetKind.Structure)
            {
                Connection.RemoveStructure(Menu.Id);
                Menu = null;
                Selected = null;
            }
        }

        public void Bake(int stackIndex)
        {
            Connection.Bake(stackIndex);
        }

        /// <summary>Kolik kusů daného materiálu je v batohu.</summary>
        public int Count(ItemKind kind)
        {
            return Connection.Inventory.Stacks.Where(s => s.Kind == kind).Sum(s => s.Count);
        }

        /// <summary>Oheň z nabídky podle čísla (null, když už není).</summary>
        /// <summary>Zvíře v okolí podle čísla (null, když ho server už neposílá: zmizelo nebo je daleko).</summary>
        public AnimalView? AnimalOf(int animalId)
        {
            foreach (var animal in Connection.Animals)
            {
                if (animal.Id == animalId)
                {
                    return animal;
                }
            }

            return null;
        }

        /// <summary>Kde cíl je teď. Zvíře se hýbe, ostatní cíle stojí na místě.</summary>
        public Vector2 PositionOf(InteractionTarget target)
        {
            if (target.Kind == TargetKind.Animal)
            {
                var animal = AnimalOf(target.Id);
                if (animal.HasValue)
                {
                    return new Vector2(animal.Value.X, animal.Value.Y);
                }
            }

            if (target.Kind == TargetKind.Stranger)
            {
                var stranger = StrangerOf(target.Id);
                if (stranger.HasValue)
                {
                    return new Vector2(stranger.Value.X, stranger.Value.Y);
                }
            }

            if (target.Kind == TargetKind.Player && Connection.Snapshot != null)
            {
                foreach (var player in Connection.Snapshot.Players)
                {
                    if (player.Id == target.Id)
                    {
                        return new Vector2(player.X, player.Y);
                    }
                }
            }

            return new Vector2((float)target.X, (float)target.Y);
        }

        /// <summary>Zaútočit na zvíře v nabídce, jinak na nejbližší živé zvíře na dosah (mezerník).</summary>
        public void Attack()
        {
            if (Menu != null && Menu.Kind == TargetKind.Animal)
            {
                Connection.Attack(Menu.Id);
                return;
            }

            var self = Connection.SelfSnapshot();
            if (self == null)
            {
                return;
            }

            var best = AttackReach;
            var found = -1;
            foreach (var animal in Connection.Animals)
            {
                var d = Vector2.Distance(new Vector2(animal.X, animal.Y), new Vector2(self.Value.X, self.Value.Y));
                if (animal.State != AnimalState.Dead && d < best)
                {
                    best = d;
                    found = animal.Id;
                }
            }

            if (found >= 0)
            {
                Connection.Attack(found);
            }
        }

        /// <summary>Stáhnout maso ze zvířete v nabídce.</summary>
        public void Butcher()
        {
            if (Menu != null && Menu.Kind == TargetKind.Animal)
            {
                Connection.Butcher(Menu.Id);
                Menu = null;
                Selected = null;
            }
        }

        public void MakeSpear()
        {
            Connection.MakeSpear();
        }

        public void MakeAxe()
        {
            Connection.MakeAxe();
        }

        /// <summary>Strom na dlaždici stojí (nepadá a není z něj pařez).</summary>
        public bool TreeStanding(int tileIndex)
        {
            foreach (var state in Connection.TreeStates)
            {
                if (state.TileIndex == tileIndex) return false;
            }

            return true;
        }

        /// <summary>Seknout sekerou do stromu, u kterého je otevřená nabídka.</summary>
        public void ChopTree()
        {
            if (Menu != null && Menu.Kind == TargetKind.Tree)
            {
                Connection.ChopTree(Menu.TileX, Menu.TileY);
            }
        }

        /// <summary>Dřevo z nabídky (kmen nebo klad), nebo null, když už na zemi není.</summary>
        public TimberView? MenuTimber()
        {
            if (Menu == null || Menu.Kind != TargetKind.Timber)
            {
                return null;
            }

            foreach (var timber in Connection.Timbers)
            {
                if (timber.Id == Menu.Id) return timber;
            }

            return null;
        }

        /// <summary>Sekerou do dřeva z nabídky: z kmene odsekne klad, klad rozštípe na dřevo.</summary>
        public void ChopTimber()
        {
            if (Menu != null && Menu.Kind == TargetKind.Timber) Connection.ChopTimber(Menu.Id);
        }

        public void PickUpTimber()
        {
            if (Menu != null && Menu.Kind == TargetKind.Timber)
            {
                Connection.PickUpTimber(Menu.Id);
                Menu = null;
                Selected = null;
            }
        }

        /// <summary>Rybařit s kopím u vody, u které je otevřená nabídka.</summary>
        public void Fish()
        {
            if (Menu != null && Menu.Kind == TargetKind.Water)
            {
                Connection.Fish(Menu.TileX, Menu.TileY);
            }
        }

        /// <summary>Vzít úlovek z pasti, u které je otevřená nabídka.</summary>
        public void TakeCatch()
        {
            if (Menu != null && Menu.Kind == TargetKind.Structure)
            {
                Connection.TakeCatch(Menu.Id);
            }
        }

        /// <summary>Stavba, u které je otevřená nabídka (null, když už není).</summary>
        public StructureView? MenuStructure()
        {
            if (Menu == null || Menu.Kind != TargetKind.Structure)
            {
                return null;
            }

            foreach (var structure in Connection.Structures)
            {
                if (Menu != null && structure.Id == Menu.Id)
                {
                    return structure;
                }
            }

            return null;
        }

        /// <summary>Tělo v otevřené nabídce, nebo null.</summary>
        public CorpseView MenuCorpse()
        {
            if (Menu == null || Menu.Kind != TargetKind.Corpse)
            {
                return null;
            }

            return Connection.Corpses.FirstOrDefault(c => c.Id == Menu.Id);
        }

        /// <summary>Smím z těla brát? Moje tělo a tělo z mého kmene hned, cizí až po 24 hodinách (server to ověří).</summary>
        public bool MayLoot(CorpseView corpse)
        {
            return InMyTribe(corpse.OwnerId) || Clock.TotalMinutes >= corpse.TribeOnlyUntil;
        }

        /// <summary>Hromada kamenů na dlaždici, ve které ještě něco je, nebo null.</summary>
        public RockPileView? RockPileAt(int tileX, int tileY)
        {
            var world = Connection.World;
            if (world == null)
            {
                return null;
            }

            var index = tileY * world.Width + tileX;
            foreach (var pile in Connection.RockPiles)
            {
                if (pile.TileIndex == index && pile.Stones > 0)
                {
                    return pile;
                }
            }

            return null;
        }

        /// <summary>Název stavby do nabídky: u dekorace její jméno ze serveru (Lavice…), jinak druh stavby.</summary>
        public string StructureName(StructureView structure)
        {
            if (structure.Kind == BuildKind.Decoration)
            {
                var decoration = Connection.DecorationTypes.FirstOrDefault(d => d.Id == structure.Decoration);
                if (decoration != null)
                {
                    return decoration.Name;
                }
            }

            return StructureViews.NameOf(structure.Kind);
        }

        /// <summary>Je hráč se mnou v kmeni (nebo jsem to já)?</summary>
        public bool InMyTribe(int playerId)
        {
            var members = Connection.TribeMembers;
            return playerId == Connection.PlayerId || (members.Contains(playerId) && members.Contains(Connection.PlayerId));
        }

        /// <summary>Smím sahat na stavbu (rozebrat, vzít úlovek, zásoby)? Jen svou a svého kmene (návrh 14.2).</summary>
        public bool MayTouch(StructureView structure)
        {
            return structure.OwnerId < 0 || InMyTribe(structure.OwnerId);
        }

        /// <summary>Obsah zásob kmene v otevřené nabídce, nebo null (není to koš, nebo cizí).</summary>
        public StockpileView MenuStockpile()
        {
            var structure = MenuStructure();
            if (structure == null || structure.Value.Kind != BuildKind.Stockpile)
            {
                return null;
            }

            return Connection.Stockpiles.FirstOrDefault(s => s.StructureId == structure.Value.Id);
        }

        /// <summary>Cizinec podle čísla, nebo null (je daleko a server ho neposílá).</summary>
        public StrangerView? StrangerOf(int strangerId)
        {
            foreach (var stranger in Connection.Strangers)
            {
                if (stranger.Id == strangerId)
                {
                    return stranger;
                }
            }

            return null;
        }

        /// <summary>Nejbližší cizinec k hráči do maxDistance (metry), nebo null.</summary>
        public StrangerView? NearestStranger(float maxDistance)
        {
            var self = Connection.SelfSnapshot();
            if (!self.HasValue)
            {
                return null;
            }

            StrangerView? best = null;
            var bestDistance = maxDistance;
            foreach (var stranger in Connection.Strangers)
            {
                var distance = Vector2.Distance(new Vector2(stranger.X, stranger.Y), new Vector2(self.Value.X, self.Value.Y));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = stranger;
                }
            }

            return best;
        }

        /// <summary>Komu hráč dává věc z batohu (číslo cizince), nebo -1. Batoh pak u věcí ukazuje „Dát“.</summary>
        public int GiveTo { get; set; } = -1;

        /// <summary>Otevře batoh, aby hráč vybral, co dát cizinci v nabídce.</summary>
        public void StartGiving()
        {
            if (Menu != null && Menu.Kind == TargetKind.Stranger)
            {
                GiveTo = Menu.Id;
                BackpackOpen = true;
            }
        }

        /// <summary>Dá cizinci jednu věc z hromádky a ukončí dávání.</summary>
        public void Give(int stackIndex)
        {
            if (GiveTo >= 0)
            {
                Connection.GiveToStranger(GiveTo, stackIndex);
            }

            GiveTo = -1;
        }

        /// <summary>Ukázat cizinci rostlinu v nabídce (gesto ukázat). Cizinec řekne, jestli se jí.</summary>
        public void ShowPlantToStranger()
        {
            if (Menu != null && Menu.Kind == TargetKind.Plant)
            {
                Connection.MakeGesture(Gesture.Point, (float)Menu.X, (float)Menu.Y);
            }
        }

        /// <summary>Pozvat hráče v nabídce do kmene.</summary>
        public void InviteToTribe()
        {
            if (Menu != null && Menu.Kind == TargetKind.Player)
            {
                Connection.InviteToTribe(Menu.Id);
            }
        }

        /// <summary>Značka v otevřené nabídce, nebo null (mezitím zvětrala nebo ji autor smazal).</summary>
        public SignView MenuSign()
        {
            if (Menu == null || Menu.Kind != TargetKind.Sign)
            {
                return null;
            }

            return Connection.Signs.FirstOrDefault(s => s.Id == Menu.Id);
        }

        /// <summary>Smazat vlastní značku v nabídce.</summary>
        public void EraseSign()
        {
            if (Menu != null && Menu.Kind == TargetKind.Sign)
            {
                Connection.EraseSign(Menu.Id);
                Menu = null;
                Selected = null;
            }
        }

        public FireView? MenuFire()
        {
            if (Menu == null || Menu.Kind != TargetKind.Fire)
            {
                return null;
            }

            foreach (var fire in Connection.Fires)
            {
                if (fire.Id == Menu.Id)
                {
                    return fire;
                }
            }

            return null;
        }

        /// <summary>Lehnout si ke spánku, nebo vstát. Jestli postava usne, rozhodne server (únava, teplo).</summary>
        public void ToggleSleep()
        {
            Menu = null;
            Selected = null;
            Connection.ToggleSleep();
        }

        /// <summary>Jsem v kmeni? (Po smrti se pak můžu narodit u něj.)</summary>
        public bool HasTribe
        {
            get { return Connection.TribeMembers.Contains(Connection.PlayerId); }
        }

        /// <summary>Narodit se znovu. U kmene to oznámí server (místo se nemusí najít).</summary>
        public void Reborn(BirthPlace place = BirthPlace.Wild)
        {
            Connection.Reborn(place);
            Menu = null;
            Selected = null;
            _log.Clear();
            if (place == BirthPlace.Wild)
            {
                Say("Probouzíš se na neznámém místě. Nic nevíš.", false);
            }
        }

        public void Say(string text, bool warn)
        {
            _log.Add(new LogLine(text, warn, Time.unscaledTime));
            if (_log.Count > MaxLogLines)
            {
                _log.RemoveAt(0);
            }
        }
    }
}
