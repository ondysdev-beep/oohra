using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Pochodeň v ruce postavy: klacek, plamínek, který svítí sám, slabý kouř a teplé mihotavé světlo kolem.
    /// Vidí ji všichni hráči (PlayerSnapshot.Torch). Ve tmě a v mlze je to často jediné světlo široko daleko.
    /// </summary>
    public class HandTorch : MonoBehaviour
    {
        public const string ObjectName = "Pochodeň";
        private const float LightRange = 7f;
        private const float LightIntensity = 2.2f;
        private static readonly Color LightColor = new Color(1f, 0.6f, 0.28f);

        private Light _light;
        private float _phase;

        /// <summary>Svítí pochodeň (je vidět)?</summary>
        public bool Lit
        {
            get { return gameObject.activeSelf; }
        }

        /// <summary>Přidá pochodeň k postavě (kořen postavy je v půlce výšky). Zatím zhasnutou.</summary>
        public static HandTorch Attach(Transform figure)
        {
            var torch = new GameObject(ObjectName);
            torch.transform.SetParent(figure, false);
            // Pravá ruka, trochu před tělem, pochodeň mírně nakloněná dopředu.
            torch.transform.localPosition = new Vector3(0.3f, 0.05f, 0.18f);
            torch.transform.localRotation = Quaternion.Euler(15f, 0f, -8f);

            // Pochodeň z katalogu (Torch), srovnaná na 0,6 m, jinak klacek z kódu. Plamen je vždy nahoře.
            var model = VisualCatalog.Current.Torch;
            if (model != null)
            {
                var stick = VisualCatalog.Spawn(model, torch.transform, "Klacek", height: 0.6f);
                stick.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            }
            else
            {
                var stick = IslandView.Primitive(PrimitiveType.Cylinder, "Klacek", torch.transform, Palette.Hex("#4a3424"));
                stick.transform.localScale = new Vector3(0.05f, 0.3f, 0.05f);
                stick.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            }

            var flame = IslandView.Primitive(PrimitiveType.Sphere, "Plamínek", torch.transform, Color.white);
            Palette.SetGlow(flame, Palette.Hex("#f2a03a"));
            flame.transform.localScale = new Vector3(0.13f, 0.2f, 0.13f);
            flame.transform.localPosition = new Vector3(0f, 0.47f, 0f);

            ParticleLook.CreateSmoke(torch.transform, new Vector3(0f, 0.6f, 0f), 0.35f);

            var view = torch.AddComponent<HandTorch>();
            view._light = new GameObject("Světlo pochodně").AddComponent<Light>();
            view._light.transform.SetParent(torch.transform, false);
            view._light.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            view._light.type = LightType.Point;
            view._light.color = LightColor;
            view._light.range = LightRange;
            view._light.intensity = LightIntensity;
            view._light.shadows = LightShadows.None;
            view._phase = Random.value * 10f;
            torch.SetActive(false);
            return view;
        }

        private void Update()
        {
            var t = Time.time + _phase;
            _light.intensity = LightIntensity * (1f + 0.12f * Mathf.Sin(t * 11f) + 0.07f * Mathf.Sin(t * 27f));
        }
    }
}
