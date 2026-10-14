using UnityEngine;

namespace Oohra.Client
{
    /// <summary>Na co se dá kliknout.</summary>
    public enum TargetKind
    {
        Plant,
        Water,
        Tree,
        Rock,
        Fire,
        Structure,
        Animal,
        Sign,
        Player,
        Corpse,
    }

    /// <summary>Na co hráč kliknul: rostlina, oheň, stavba nebo zvíře (číslo), nebo dlaždice (voda, strom, skála).</summary>
    public sealed class InteractionTarget
    {
        private InteractionTarget(TargetKind kind, int id, int tileX, int tileY, double x, double y)
        {
            Kind = kind;
            Id = id;
            TileX = tileX;
            TileY = tileY;
            X = x;
            Y = y;
        }

        public TargetKind Kind { get; }

        public bool IsPlant => Kind == TargetKind.Plant;

        /// <summary>Číslo rostliny, ohně, stavby nebo zvířete.</summary>
        public int Id { get; }

        public int PlantId => Id;

        public int TileX { get; }

        public int TileY { get; }

        /// <summary>Místo na mapě, ke kterému postava jde.</summary>
        public double X { get; }

        public double Y { get; }

        public Vector3 Position => WorldCoords.ToUnity(X, Y);

        /// <summary>
        /// Jak blízko musí postava dojít (jako v Core: gathering.json, fire.json a building.json).
        /// Server dosah stejně ověří, tady jen kvůli tomu, kdy přestat jít a otevřít nabídku.
        /// </summary>
        public double Reach
        {
            get
            {
                switch (Kind)
                {
                    case TargetKind.Plant: return 0.95;
                    case TargetKind.Water: return 1.7;
                    case TargetKind.Tree: return 1.5;
                    case TargetKind.Structure: return 1.6;
                    case TargetKind.Animal: return 1.4;
                    case TargetKind.Player: return 2.5;
                    default: return 1.3;
                }
            }
        }

        public static InteractionTarget ForPlant(int plantId, double x, double y)
        {
            return new InteractionTarget(TargetKind.Plant, plantId, 0, 0, x, y);
        }

        public static InteractionTarget ForFire(int fireId, double x, double y)
        {
            return new InteractionTarget(TargetKind.Fire, fireId, (int)System.Math.Floor(x), (int)System.Math.Floor(y), x, y);
        }

        /// <summary>
        /// Zvíře podle čísla. (x, y) je poloha při kliknutí. Zvíře se hýbe, živou polohu dává ClientSession.PositionOf.
        /// </summary>
        public static InteractionTarget ForAnimal(int animalId, double x, double y)
        {
            return new InteractionTarget(TargetKind.Animal, animalId, (int)System.Math.Floor(x), (int)System.Math.Floor(y), x, y);
        }

        /// <summary>Stavba (zeď) podle čísla. (x, y) je její střed.</summary>
        public static InteractionTarget ForStructure(int structureId, double x, double y)
        {
            return new InteractionTarget(TargetKind.Structure, structureId, (int)System.Math.Floor(x), (int)System.Math.Floor(y), x, y);
        }

        public static InteractionTarget ForSign(int signId, double x, double y)
        {
            return new InteractionTarget(TargetKind.Sign, signId, (int)System.Math.Floor(x), (int)System.Math.Floor(y), x, y);
        }

        /// <summary>Tělo mrtvého hráče (číslo těla).</summary>
        public static InteractionTarget ForCorpse(int corpseId, double x, double y)
        {
            return new InteractionTarget(TargetKind.Corpse, corpseId, 0, 0, x, y);
        }

        /// <summary>Jiný hráč (číslo hráče). Hýbe se, živou polohu dává ClientSession.PositionOf.</summary>
        public static InteractionTarget ForPlayer(int playerId, double x, double y)
        {
            return new InteractionTarget(TargetKind.Player, playerId, 0, 0, x, y);
        }

        /// <summary>Dlaždice: voda, strom nebo skála.</summary>
        public static InteractionTarget ForTile(TargetKind kind, int tileX, int tileY)
        {
            return new InteractionTarget(kind, -1, tileX, tileY, tileX + 0.5, tileY + 0.5);
        }
    }
}
