using Oohra.Core.World;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>Na co hráč kliknul: rostlina, nebo vodní dlaždice.</summary>
    public sealed class InteractionTarget
    {
        private InteractionTarget(bool isPlant, PlantInstance plant, int tileX, int tileY, Vector3 position)
        {
            IsPlant = isPlant;
            Plant = plant;
            TileX = tileX;
            TileY = tileY;
            Position = position;
        }

        public bool IsPlant { get; }

        public PlantInstance Plant { get; }

        public int TileX { get; }

        public int TileY { get; }

        /// <summary>Místo v Unity, ke kterému postava jde.</summary>
        public Vector3 Position { get; }

        public static InteractionTarget ForPlant(PlantInstance plant)
        {
            return new InteractionTarget(true, plant, 0, 0, WorldCoords.ToUnity(plant.X, plant.Y));
        }

        public static InteractionTarget ForWater(int tileX, int tileY)
        {
            return new InteractionTarget(false, default, tileX, tileY, WorldCoords.ToUnity(tileX + 0.5, tileY + 0.5));
        }
    }
}
