using Oohra.Core.Plants;
using Oohra.Core.World;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Udržuje vzhled rostlin podle stavu světa: vykopaná rostlina zmizí, plodící má plod
    /// v barvě podle zralosti (zelený, zralý v barvě druhu, scvrklý tmavý). Barvy z prototypu.
    /// </summary>
    public class PlantViews : MonoBehaviour
    {
        private const float RefreshSeconds = 0.5f;

        private GameState _state;
        private GameObject[] _plants;
        private GameObject[] _fruits;
        private float _nextRefresh;

        public void Init(GameState state, GameObject[] plants, GameObject[] fruits)
        {
            _state = state;
            _plants = plants;
            _fruits = fruits;
            Refresh();
        }

        private void Update()
        {
            if (_state == null || Time.unscaledTime < _nextRefresh)
            {
                return;
            }

            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            Refresh();
        }

        private void Refresh()
        {
            var island = _state.Island;
            var now = _state.Clock.TotalMinutes;
            for (var i = 0; i < _plants.Length; i++)
            {
                var plant = island.Plants[i];
                var present = !_state.PlantStates.IsGone(plant.Id, now);
                _plants[i].SetActive(present);

                var stage = present ? Gathering.FruitStage(island, _state.PlantStates, plant, _state.Clock) : null;
                _fruits[i].SetActive(stage.HasValue);
                if (stage.HasValue)
                {
                    Palette.SetColor(_fruits[i], FruitColor(island.Species[plant.SpeciesId], stage.Value));
                }
            }
        }

        private static Color FruitColor(PlantSpecies species, Ripeness ripeness)
        {
            switch (ripeness)
            {
                case Ripeness.Unripe: return Palette.Hex("#a8b86e");
                case Ripeness.Overripe: return Palette.Hex("#5a4332");
                default: return Palette.Hsl(species.FruitHue, 68, 52);
            }
        }
    }
}
