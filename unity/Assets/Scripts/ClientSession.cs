using System.Collections.Generic;
using Oohra.Core.Plants;
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

        /// <summary>Nabídka u rostliny nebo vody, když k ní postava došla.</summary>
        public InteractionTarget Menu { get; set; }

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
                }
            }
        }

        public void Drink()
        {
            if (Menu != null && !Menu.IsPlant)
            {
                Connection.Drink(Menu.TileX, Menu.TileY);
            }
        }

        public void Reborn()
        {
            Connection.Reborn();
            Menu = null;
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
