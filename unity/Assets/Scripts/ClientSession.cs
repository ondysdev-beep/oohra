using System.Collections.Generic;
using System.Linq;
using Oohra.Core.Items;
using Oohra.Core.Plants;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
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

        public bool BackpackOpen { get; set; }

        /// <summary>Panel dovedností (klávesa K).</summary>
        public bool SkillsOpen { get; set; }

        public bool Sleeping => Connection.Self != null && Connection.Self.Sleeping;

        /// <summary>Nabídka u rostliny nebo vody, když k ní postava došla.</summary>
        public InteractionTarget Menu { get; set; }

        /// <summary>Vybraná věc (pod ní je kruh): na co hráč kliknul, dokud nejde jinam.</summary>
        public InteractionTarget Selected { get; set; }

        public bool Dead => Connection.Self != null && Connection.Self.Dead;

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
                bool warn;
                var text = Texts.For(gameEvent, out warn);
                if (text != null)
                {
                    Say(text, warn);
                }
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

        public void Reborn()
        {
            Connection.Reborn();
            Menu = null;
            Selected = null;
            _log.Clear();
            Say("Probouzíš se na neznámém místě. Nic nevíš.", false);
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
