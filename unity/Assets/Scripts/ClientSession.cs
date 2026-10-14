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
        public void Place(BuildKind kind, double x, double y, float angle, float length = 1f)
        {
            Connection.Place(kind, (float)x, (float)y, angle, length);
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
        /// <summary>Druh stavby, u které je otevřená nabídka (zeď, zeď s otvorem, dveře).</summary>
        public BuildKind MenuStructureKind()
        {
            foreach (var structure in Connection.Structures)
            {
                if (Menu != null && structure.Id == Menu.Id)
                {
                    return structure.Kind;
                }
            }

            return BuildKind.StickWall;
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
