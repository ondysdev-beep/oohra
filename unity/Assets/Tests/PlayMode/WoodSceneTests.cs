using System.Collections;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Oohra.Core.Items;
using Oohra.Core.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Oohra.Client.Tests.PlayTestTools;

namespace Oohra.Client.Tests
{
    /// <summary>
    /// Kácení ve hře: postava si vyrobí sekeru, dojde ke stromu, kácí ho, strom spadne (zbude pařez a ležící kmen),
    /// odsekne klad, zvedne ho a nese na rameni. Snímek unity/Logs/kaceni.png.
    /// </summary>
    public class WoodSceneTests
    {
        private const int TestPort = 9188;

        private Process _server;

        [UnitySetUp]
        public IEnumerator StartServer()
        {
            _server = PlayTestTools.StartServer(TestPort, "\"startSticks\": 1, \"startStones\": 2");
            SceneManager.LoadScene("Ostrov");
            yield return new WaitForSecondsRealtime(2f);
        }

        [UnityTearDown]
        public IEnumerator StopServer()
        {
            PlayTestTools.StopServer(_server);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FellTreeCutPieceAndCarry()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var session = bootstrap.Session;
            var connection = session.Connection;
            yield return WaitFor(() => connection.SelfSnapshot().HasValue, 5f, "Postava se neobjevila.");

            session.MakeAxe();
            yield return WaitFor(() => session.Count(ItemKind.Axe) == 1, 5f, "Sekera se nevyrobila.");

            // Nejbližší strom: postava k němu dojde jako po kliknutí a otevře nabídku.
            var self = connection.SelfSnapshot().Value;
            var world = connection.World;
            var tree = world.Trees
                .OrderBy(t => Mathf.Pow(t.TileIndex % world.Width + 0.5f - self.X, 2) + Mathf.Pow(t.TileIndex / world.Width + 0.5f - self.Y, 2))
                .First();
            var controller = Object.FindAnyObjectByType<PlayerController>();
            controller.WalkTo(InteractionTarget.ForTile(TargetKind.Tree, tree.TileIndex % world.Width, tree.TileIndex / world.Width));
            yield return WaitFor(() => session.Menu != null, 30f, "Postava ke stromu nedošla.");

            var trees = Object.FindAnyObjectByType<TreeViews>();
            var timbers = Object.FindAnyObjectByType<TimberViews>();
            var watch = Stopwatch.StartNew();
            while (trees.StumpAt(tree.TileIndex) == null && watch.Elapsed.TotalSeconds < 30)
            {
                session.ChopTree();
                yield return new WaitForSecondsRealtime(0.8f);
            }

            Assert.IsNotNull(trees.StumpAt(tree.TileIndex), "Strom nespadl. Hlášky: " + string.Join(" | ", session.Log.Select(l => l.Text)));
            Assert.IsFalse(trees.TreeAt(tree.TileIndex).activeSelf);
            yield return WaitFor(() => timbers.Count == 1, 3f, "Ležící kmen se neobjevil.");
            yield return SaveScreenshot("kaceni.png");

            var trunk = connection.Timbers.Single();
            controller.WalkTo(InteractionTarget.ForTimber(trunk.Id, trunk.X, trunk.Y));
            yield return WaitFor(() => session.Menu != null, 20f, "Postava ke kmeni nedošla.");
            session.ChopTimber();
            yield return WaitFor(() => connection.Timbers.Any(t => t.Kind == TimberKind.Piece), 5f, "Klad se neodsekl.");

            var piece = connection.Timbers.First(t => t.Kind == TimberKind.Piece);
            controller.WalkTo(InteractionTarget.ForTimber(piece.Id, piece.X, piece.Y));
            yield return WaitFor(() => session.Menu != null, 20f, "Postava ke kladu nedošla.");
            session.PickUpTimber();
            var players = Object.FindAnyObjectByType<PlayerViews>();
            yield return WaitFor(() => players.IsCarrying(connection.PlayerId), 5f, "Postava klad nenese.");
        }
    }
}
