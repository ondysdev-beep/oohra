using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Vzhled rostlin podle zpráv serveru: vykopaná rostlina zmizí, plodící má plod
    /// v barvě podle zralosti (zelený, zralý v barvě druhu, scvrklý tmavý). Barvy z prototypu.
    /// </summary>
    public class PlantViews : MonoBehaviour
    {
        private const float RefreshSeconds = 0.25f;

        private WorldView _world;
        private GameConnection _connection;
        private GameObject[] _plants;
        private GameObject[] _fruits;
        private PlantVisual[] _shown;
        private float _nextRefresh;

        public void Init(WorldView world, GameConnection connection, GameObject[] plants, GameObject[] fruits)
        {
            _world = world;
            _connection = connection;
            _plants = plants;
            _fruits = fruits;
            _shown = new PlantVisual[plants.Length];
            Refresh(force: true);
        }

        private void Update()
        {
            if (_connection == null || Time.unscaledTime < _nextRefresh)
            {
                return;
            }

            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            Refresh(force: false);
        }

        private void Refresh(bool force)
        {
            var visuals = _connection.PlantVisuals;
            for (var i = 0; i < _plants.Length && i < visuals.Length; i++)
            {
                if (!force && visuals[i] == _shown[i])
                {
                    continue;
                }

                _shown[i] = visuals[i];
                _plants[i].SetActive(visuals[i] != PlantVisual.Gone);
                var hasFruit = visuals[i] == PlantVisual.UnripeFruit || visuals[i] == PlantVisual.RipeFruit || visuals[i] == PlantVisual.OverripeFruit;
                _fruits[i].SetActive(hasFruit);
                if (hasFruit)
                {
                    Palette.SetColor(_fruits[i], FruitColor(_world.Species[_world.Plants[i].SpeciesId], visuals[i]));
                }
            }
        }

        private static Color FruitColor(SpeciesView species, PlantVisual visual)
        {
            switch (visual)
            {
                case PlantVisual.UnripeFruit: return Palette.Hex("#a8b86e");
                case PlantVisual.OverripeFruit: return Palette.Hex("#5a4332");
                default: return Palette.Hsl(species.FruitHue, 68, 52);
            }
        }
    }
}
