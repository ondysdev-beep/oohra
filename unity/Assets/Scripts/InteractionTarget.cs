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
    }

    /// <summary>Na co hráč kliknul: rostlina nebo oheň (číslo), nebo dlaždice (voda, strom, skála).</summary>
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

        /// <summary>Číslo rostliny nebo ohně.</summary>
        public int Id { get; }

        public int PlantId => Id;

        public int TileX { get; }

        public int TileY { get; }

        /// <summary>Místo na mapě, ke kterému postava jde.</summary>
        public double X { get; }

        public double Y { get; }

        public Vector3 Position => WorldCoords.ToUnity(X, Y);

        /// <summary>
        /// Jak blízko musí postava dojít (jako v Core: gathering.json a fire.json).
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
                    default: return 1.3;
                }
            }
        }

        public static InteractionTarget ForPlant(int plantId, double x, double y)
        {
            return new InteractionTarget(TargetKind.Plant, plantId, 0, 0, x, y);
        }

        public static InteractionTarget ForFire(int fireId, int tileX, int tileY)
        {
            return new InteractionTarget(TargetKind.Fire, fireId, tileX, tileY, tileX + 0.5, tileY + 0.5);
        }

        /// <summary>Dlaždice: voda, strom nebo skála.</summary>
        public static InteractionTarget ForTile(TargetKind kind, int tileX, int tileY)
        {
            return new InteractionTarget(kind, -1, tileX, tileY, tileX + 0.5, tileY + 0.5);
        }
    }
}
