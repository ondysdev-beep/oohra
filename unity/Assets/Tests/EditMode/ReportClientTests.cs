using System;
using NUnit.Framework;
using Oohra.Core.Social;
using Oohra.Protocol;

namespace Oohra.Client.Tests
{
    /// <summary>Tlačítko Nahlásit v klientu: texty, důvody a panel.</summary>
    public class ReportClientTests
    {
        [Test]
        public void EveryResultAndReasonHasText()
        {
            foreach (ReportResult result in Enum.GetValues(typeof(ReportResult)))
            {
                Assert.AreNotEqual(result.ToString(), Texts.For(result));
            }

            foreach (ReportReason reason in Enum.GetValues(typeof(ReportReason)))
            {
                Assert.AreNotEqual(reason.ToString(), Texts.ReasonName(reason));
            }
        }

        [Test]
        public void ReasonsFitTarget()
        {
            CollectionAssert.Contains(ReportHud.ReasonsFor(ReportTarget.Player), ReportReason.Behavior);
            CollectionAssert.Contains(ReportHud.ReasonsFor(ReportTarget.Sign), ReportReason.OffensiveSign);
            CollectionAssert.DoesNotContain(ReportHud.ReasonsFor(ReportTarget.Sign), ReportReason.Cheating);
            CollectionAssert.Contains(ReportHud.ReasonsFor(ReportTarget.General), ReportReason.Bug);
        }

        /// <summary>Panel se otevře s cílem a po odeslání (výběru důvodu) zavře.</summary>
        [Test]
        public void SendingClosesPanel()
        {
            var session = new ClientSession(new GameConnection());
            session.AskReport(ReportTarget.Player, 7, "hráče Petr");

            Assert.AreEqual(ReportTarget.Player, session.PendingReport.Target);
            Assert.AreEqual(7, session.PendingReport.TargetId);
            session.SendReport(ReportReason.Behavior);

            Assert.IsNull(session.PendingReport);
        }

        [Test]
        public void ReportEventIsWarningUnlessSent()
        {
            bool warn;
            Texts.For(new GameEvent(EventKind.Report, (byte)ReportResult.Sent, default, new Oohra.Core.Characters.TasteNote[0]), out warn);
            Assert.IsFalse(warn);
            Texts.For(new GameEvent(EventKind.Report, (byte)ReportResult.TooOften, default, new Oohra.Core.Characters.TasteNote[0]), out warn);
            Assert.IsTrue(warn);
        }
    }
}
