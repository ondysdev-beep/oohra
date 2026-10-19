using System.Collections;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Oohra.Core.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Oohra.Client.Tests.PlayTestTools;

namespace Oohra.Client.Tests
{
    /// <summary>Režim stavby: modulární napojování zdí a dveře, které se otevřou, když jimi postava projde.</summary>
    public class BuildingSceneTests
    {
        private const int TestPort = 9180;

        private Process _server;

        [UnitySetUp]
        public IEnumerator StartServer()
        {
            _server = PlayTestTools.StartServer(TestPort, "\"startSticks\": 30");
            SceneManager.LoadScene("Ostrov");
            yield return new WaitForSecondsRealtime(2f);
        }

        [UnityTearDown]
        public IEnumerator StopServer()
        {
            PlayTestTools.StopServer(_server);
            yield return null;
        }

        /// <summary>
        /// Stavební mřížka: první zeď založí plán, druhá (kurzor kousek za koncem, trochu vedle) leží přesně na sousední
        /// hraně políčka a pokračuje rovně. Mřížka je při stavění vidět.
        /// </summary>
        [UnityTest]
        public IEnumerator GridWallJoinsWallEnd()
        {
            var session = Object.FindAnyObjectByType<IslandBootstrap>();
            yield return Join(session);
            var buildMode = Object.FindAnyObjectByType<BuildMode>();
            var connection = session.Session.Connection;

            buildMode.Open(BuildKind.StickWall);
            buildMode.AngleDegrees = 0f;
            yield return PlaceAround(session.Session, buildMode, () => connection.Structures.Length > 0);
            Assert.AreEqual(1, connection.Structures.Length, "První zeď se nepodařilo postavit.");
            var first = connection.Structures[0];
            Assert.AreEqual(1, connection.Plans.Length, "První zeď nezaložila plán.");
            Assert.AreEqual(BuildGrid.Cell, first.Length, 0.01f);

            foreach (var side in new[] { 1f, -1f })
            {
                buildMode.PlaceAt(first.X + side * 1.8f, first.Y + 0.3f);
                Assert.IsTrue(buildMode.OnExistingPlan, "Náhled se nepřichytil k plánu.");
                Assert.IsTrue(Object.FindAnyObjectByType<GridLines>().Visible, "Mřížka není vidět.");
                yield return new WaitForSecondsRealtime(0.7f);
                if (connection.Structures.Length < 2)
                {
                    continue;
                }

                var second = connection.Structures.First(s => s.Id != first.Id);
                Assert.AreEqual(first.X + side * BuildGrid.Cell, second.X, 0.01f, "Zeď neleží na sousední hraně.");
                Assert.AreEqual(first.Y, second.Y, 0.01f);
                Assert.AreEqual(first.Angle, second.Angle, 0.01f);
                Assert.AreEqual(first.PlanId, second.PlanId);
                yield return SaveScreenshot("moduly.png");
                yield break;
            }

            Assert.Fail("Druhou zeď se nepodařilo postavit ani na jednom konci.");
        }

        /// <summary>Zeď s otvorem, do ní dveře. Když postava projde otvorem, dveře se otevřou a pak zavřou.</summary>
        [UnityTest]
        public IEnumerator DoorOpensWhenWalkingThrough()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            yield return Join(bootstrap);
            var session = bootstrap.Session;
            var connection = session.Connection;
            var buildMode = Object.FindAnyObjectByType<BuildMode>();
            var views = Object.FindAnyObjectByType<StructureViews>();

            // Zeď s otvorem 1,5 m od postavy, napříč směru, kterým k ní postava půjde.
            buildMode.Open(BuildKind.DoorFrame);
            var offsets = new[] { new Vector2(1, 0), new Vector2(0, 1), new Vector2(-1, 0), new Vector2(0, -1) };
            var offset = Vector2.zero;
            foreach (var candidate in offsets)
            {
                var self = connection.SelfSnapshot().Value;
                buildMode.AngleDegrees = candidate.x != 0 ? 90f : 0f;
                buildMode.PlaceAt(self.X + candidate.x * 1.5f, self.Y + candidate.y * 1.5f);
                yield return new WaitForSecondsRealtime(0.7f);
                if (connection.Structures.Length > 0)
                {
                    offset = candidate;
                    break;
                }
            }

            Assert.AreEqual(1, connection.Structures.Length, "Zeď s otvorem se nepodařilo postavit.");
            var frame = connection.Structures[0];

            buildMode.Open(BuildKind.Door);
            buildMode.PlaceAt(frame.X + 0.2f, frame.Y + 0.1f);
            yield return WaitFor(() => connection.Structures.Length == 2, 3f, "Dveře se do otvoru nevsadily.");
            var door = connection.Structures.First(s => s.Kind == BuildKind.Door);
            Assert.AreEqual(frame.X, door.X, 0.01f);
            buildMode.Close();

            yield return WaitFor(() => views.DoorOf(door.Id) != null, 3f, "Dveře se ve scéně neobjevily.");
            var swing = views.DoorOf(door.Id);

            // Projít otvorem.
            var opened = 0f;
            var photographed = false;
            var until = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < until)
            {
                connection.SendInput(offset.x, offset.y);
                opened = Mathf.Max(opened, Mathf.Abs(swing.Angle));
                var self = connection.SelfSnapshot().Value;
                if (Vector2.Dot(new Vector2(self.X - frame.X, self.Y - frame.Y), offset) > 1.2f)
                {
                    break;
                }

                if (!photographed && Mathf.Abs(swing.Angle) > 40f)
                {
                    photographed = true;
                    yield return SaveScreenshot("dvere.png");
                }

                yield return null;
            }

            connection.SendInput(0, 0);
            var passed = connection.SelfSnapshot().Value;
            Assert.Greater(Vector2.Dot(new Vector2(passed.X - frame.X, passed.Y - frame.Y), offset), 0.5f, "Postava neprošla dveřmi.");
            Assert.Greater(opened, 40f, "Dveře se neotevřely.");

            yield return WaitFor(() => Mathf.Abs(swing.Angle) < 5f, 5f, "Dveře se po průchodu nezavřely.");
        }

        private static IEnumerator Join(IslandBootstrap bootstrap)
        {
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            yield return WaitFor(() => bootstrap.Session.Connection.SelfSnapshot() != null, 5f, "Chybí poloha postavy.");
        }
    }
}
