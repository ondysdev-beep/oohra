using System.Collections.Generic;
using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.Plants;
using Oohra.Core.Rng;
using Oohra.Core.Time;
using Oohra.Core.World;
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
    /// Stav hry jednoho hráče v šedé krabici: ostrov, čas, postava, batoh a hlášky.
    /// Akce (sběr, pití, jídlo) volají pravidla z Oohra.Core a výsledek převedou na hlášku.
    /// </summary>
    public sealed class GameState
    {
        private const int MaxLogLines = 6;

        private readonly List<LogLine> _log = new List<LogLine>();
        private readonly int _seed;
        private int _lives;

        public GameState(Island island, int seed, int generation)
        {
            Island = island;
            _seed = seed;
            Clock = GameClock.FromSettings();
            PlantStates = new PlantStates();
            StartLife(generation);
        }

        public Island Island { get; }

        public GameClock Clock { get; }

        public PlantStates PlantStates { get; }

        public Character Character { get; private set; }

        public Inventory Inventory { get; private set; }

        public PlayerController Player { get; set; }

        public IReadOnlyList<LogLine> Log => _log;

        /// <summary>Kam se postava narodila (pro PlayerController).</summary>
        public Position BirthPlace { get; private set; }

        /// <summary>Otevřený batoh nebo okno smrti hru zastaví (jako panely v prototypu).</summary>
        public bool BackpackOpen { get; set; }

        public bool Paused => BackpackOpen || Character.IsDead;

        /// <summary>Zrychlení herního času pro zkoušení (1, 10, 60).</summary>
        public float TimeSpeed { get; set; } = 1f;

        /// <summary>Nabídka u rostliny nebo vody, když k ní postava došla.</summary>
        public InteractionTarget Menu { get; set; }

        public void Tick(float realSeconds)
        {
            if (Paused)
            {
                return;
            }

            var minutes = Clock.Advance(realSeconds * TimeSpeed);
            Physiology.Update(Character, Clock, minutes);
            CollectBodyMessages();
        }

        public void Harvest(PlantInstance plant, PlantPart part)
        {
            var result = Gathering.Harvest(Island, PlantStates, plant, part, Clock, Inventory, Player.X, Player.Y);
            Say(Texts.For(result, part), result != HarvestResult.Picked);
            if (result == HarvestResult.Picked && part == PlantPart.Root)
            {
                Menu = null;
            }
        }

        public void Drink(int tileX, int tileY)
        {
            var result = Drinking.Drink(Character, Island, tileX, tileY, Player.X, Player.Y, Clock);
            Say(Texts.For(result), result == DrinkResult.Salty || result == DrinkResult.TooFar);
        }

        public void Eat(int stackIndex)
        {
            var item = Inventory.TakeOne(stackIndex);
            if (item == null)
            {
                return;
            }

            Eating.Eat(Character, item, Island.SpeciesSecrets[item.SpeciesId], Clock);
            CollectBodyMessages();
        }

        /// <summary>Ochutná malý kousek. Věc v batohu zůstane (jako v prototypu).</summary>
        public void Taste(int stackIndex)
        {
            if (stackIndex < 0 || stackIndex >= Inventory.Stacks.Count)
            {
                return;
            }

            var item = Inventory.Stacks[stackIndex].Item;
            Eating.Taste(Character, item, Island.SpeciesSecrets[item.SpeciesId], Clock);
            CollectBodyMessages();
        }

        public void Drop(int stackIndex)
        {
            Inventory.Drop(stackIndex);
        }

        /// <summary>Po smrti: nová postava jako další generace, jinde na ostrově. Batoh a znalosti postavy jsou pryč.</summary>
        public void Reborn()
        {
            var deathPlace = new Position(Player.X, Player.Y);
            StartLife(GenerationTraits.NextAfterDeath(Character.Traits.Generation), deathPlace);
            Player.Respawn(Character, BirthPlace);
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

        private void StartLife(int generation, Position? avoid = null)
        {
            _lives++;
            Character = new Character(generation, new Mulberry32(_seed ^ 0x0B0D ^ (_lives * 7919)));
            Inventory = Inventory.For(Character.Traits);
            BirthPlace = SpawnPoint.PickWild(Island, new Mulberry32(_seed ^ 0x5BA7 ^ (_lives * 104729)), avoid == null, avoid);
        }

        private void CollectBodyMessages()
        {
            foreach (var message in Character.TakeMessages())
            {
                if (message.Event != BodyEvent.Died)
                {
                    Say(Texts.For(message), Texts.IsWarning(message.Event));
                }
            }
        }
    }
}
