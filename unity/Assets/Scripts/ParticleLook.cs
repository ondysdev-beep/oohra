using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Materiály pro částice (kouř, déšť). Vycházejí z materiálu pěny (URP/Particles/Simple Lit, průhledný, barva
    /// z částic), takže je osvětlí slunce i oheň a v noci nesvítí. Měkké textury se kreslí v kódu, žádné soubory.
    /// </summary>
    public static class ParticleLook
    {
        private static Material _soft;
        private static Material _streak;

        /// <summary>Měkká kulatá skvrna (kouř, mlžné chuchvalce).</summary>
        public static Material Soft
        {
            get { return _soft != null ? _soft : (_soft = Create("Kouř", SoftDot(64))); }
        }

        /// <summary>Tenká svislá čárka (kapky deště natažené rychlostí).</summary>
        public static Material Streak
        {
            get { return _streak != null ? _streak : (_streak = Create("Déšť", StreakTexture(8, 64))); }
        }

        /// <summary>Renderer částic s daným materiálem, bez stínů.</summary>
        public static ParticleSystemRenderer Setup(ParticleSystem system, Material material)
        {
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        /// <summary>
        /// Kouř: tmavé chuchvalce stoupají, rostou, rozplývají se a vítr je odnáší (WeatherView.CurrentWind).
        /// scale 1 = oheň, menší = pochodeň.
        /// </summary>
        public static ParticleSystem CreateSmoke(Transform parent, Vector3 localPosition, float scale)
        {
            var go = new GameObject("Kouř");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var smoke = go.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = smoke.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f * scale, 0.8f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f * scale, 0.6f * scale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.22f, 0.22f, 0.24f, 0.4f);
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = smoke.emission;
            emission.rateOverTime = 7f * Mathf.Sqrt(scale);

            var shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10f;
            shape.radius = 0.12f * scale;

            var size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 4.5f));

            var color = smoke.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.8f, 0.8f, 0.82f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.5f, 0.55f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            var rotation = smoke.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

            var noise = smoke.noise;
            noise.enabled = true;
            noise.strength = 0.25f * scale;
            noise.frequency = 0.4f;

            var force = smoke.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;
            go.AddComponent<SmokeWind>();

            Setup(smoke, Soft);
            smoke.Play();
            return smoke;
        }

        /// <summary>
        /// Prach a listí při dopadu stromu: jednorázový oblak podél ležícího kmene (od místa from ve směru direction,
        /// délka v metrech). Sám se po pár sekundách smaže.
        /// </summary>
        public static ParticleSystem DustBurst(Vector3 from, Vector3 direction, float length)
        {
            var go = new GameObject("Prach");
            go.transform.position = from + direction.normalized * (length / 2) + Vector3.up * 0.3f;
            go.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            var dust = go.AddComponent<ParticleSystem>();
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = dust.main;
            main.loop = false;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.36f, 0.27f, 0.45f), new Color(0.3f, 0.36f, 0.2f, 0.5f));
            main.gravityModifier = -0.02f;
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = dust.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(length * 4f, 20f, 100f)) });

            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.6f, 0.3f, length);

            var size = dust.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));

            var color = dust.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.4f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            Setup(dust, Soft);
            dust.Play();
            return dust;
        }

        private static Material Create(string name, Texture2D texture)
        {
            var material = new Material(Palette.Foam) { name = name };
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            return material;
        }

        private static Texture2D SoftDot(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var half = (size - 1) / 2f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    var alpha = Mathf.Clamp01(1f - d);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D StreakTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var across = 1f - Mathf.Abs(x - (width - 1) / 2f) / (width / 2f);
                    var along = Mathf.Sin(Mathf.PI * y / (height - 1));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(across) * along));
                }
            }

            texture.Apply();
            return texture;
        }
    }
}
