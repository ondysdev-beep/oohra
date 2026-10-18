using NUnit.Framework;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Pochodeň v ruce a kouř (částice) bez serveru.</summary>
    public class WeatherClientTests
    {
        [Test]
        public void TorchHasStickFlameSmokeAndLightAndStartsOut()
        {
            var figure = new GameObject("Postava");
            try
            {
                var torch = HandTorch.Attach(figure.transform);

                Assert.IsFalse(torch.Lit);
                Assert.AreEqual(HandTorch.ObjectName, torch.name);
                var light = torch.GetComponentInChildren<Light>(true);
                Assert.AreEqual(LightType.Point, light.type);
                Assert.IsNotNull(torch.GetComponentInChildren<ParticleSystem>(true), "Pochodeň má mít kouř.");
            }
            finally
            {
                Object.DestroyImmediate(figure);
            }
        }

        [Test]
        public void SmokeUsesSoftParticleMaterial()
        {
            var parent = new GameObject("Oheň");
            try
            {
                var smoke = ParticleLook.CreateSmoke(parent.transform, Vector3.up, 1f);

                Assert.AreEqual(ParticleSystemSimulationSpace.World, smoke.main.simulationSpace);
                Assert.IsTrue(smoke.forceOverLifetime.enabled, "Kouř odnáší vítr.");
                Assert.AreSame(ParticleLook.Soft, smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial);
                Assert.IsNotNull(ParticleLook.Soft.GetTexture("_BaseMap"));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }
    }
}
