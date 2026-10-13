using UnityEngine;

namespace Oohra.Client
{
    /// <summary>Na co hráč kliknul: rostlina (číslo), nebo vodní dlaždice.</summary>
    public sealed class InteractionTarget
    {
        private InteractionTarget(bool isPlant, int plantId, int tileX, int tileY, double x, double y)
        {
            IsPlant = isPlant;
            PlantId = plantId;
            TileX = tileX;
            TileY = tileY;
            X = x;
            Y = y;
        }

        public bool IsPlant { get; }

        public int PlantId { get; }

        public int TileX { get; }

        public int TileY { get; }

        /// <summary>Místo na mapě, ke kterému postava jde.</summary>
        public double X { get; }

        public double Y { get; }

        public Vector3 Position => WorldCoords.ToUnity(X, Y);

        public static InteractionTarget ForPlant(int plantId, double x, double y)
        {
            return new InteractionTarget(true, plantId, 0, 0, x, y);
        }

        public static InteractionTarget ForWater(int tileX, int tileY)
        {
            return new InteractionTarget(false, -1, tileX, tileY, tileX + 0.5, tileY + 0.5);
        }
    }
}
