using NUnit.Framework;

namespace Oohra.Client.Tests
{
    /// <summary>Dveře se otevřou, při zavírání přeletí přes zavřenou polohu (kývají se) a pak se uklidní.</summary>
    public class DoorSwingTests
    {
        [Test]
        public void OpensThenSwingsAndSettlesClosed()
        {
            var angle = 0f;
            var velocity = 0f;
            Run(ref angle, ref velocity, 85f, 1f);
            Assert.Greater(angle, 70f, "Dveře se neotevřely.");

            var lowest = angle;
            for (var i = 0; i < 400; i++)
            {
                DoorSwing.Step(ref angle, ref velocity, 0f, 0.0125f);
                lowest = System.Math.Min(lowest, angle);
            }

            Assert.Less(lowest, -5f, "Dveře se po zavření nekývaly.");
            Assert.AreEqual(0f, angle, 3f, "Dveře se neuklidnily zavřené.");
        }

        private static void Run(ref float angle, ref float velocity, float target, float seconds)
        {
            for (var t = 0f; t < seconds; t += 0.0125f)
            {
                DoorSwing.Step(ref angle, ref velocity, target, 0.0125f);
            }
        }
    }
}
