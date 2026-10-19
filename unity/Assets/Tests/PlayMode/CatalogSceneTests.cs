using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Oohra.Client.Tests.PlayTestTools;

namespace Oohra.Client.Tests
{
    /// <summary>
    /// Ostrov s vlastními modely z katalogu vzhledu: stromy, rostliny a zeď se postaví z modelů
    /// a zprůhledňování s nimi funguje (žádná chyba v konzoli).
    /// </summary>
    public class CatalogSceneTests
    {
        private const int TestPort = 9185;

        private Process _server;
        private GameObject _templates;

        [UnitySetUp]
        public IEnumerator StartServer()
        {
            _server = PlayTestTools.StartServer(TestPort, "\"startSticks\": 4");
            VisualCatalog.Current = TestCatalog();
            SceneManager.LoadScene("Ostrov");
            yield return new WaitForSecondsRealtime(2f);
        }

        [UnityTearDown]
        public IEnumerator StopServer()
        {
            PlayTestTools.StopServer(_server);
            VisualCatalog.Current = null;
            Object.Destroy(_templates);
            yield return null;
        }

        [UnityTest]
        public IEnumerator IslandUsesCatalogModels()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var session = bootstrap.Session;
            var world = session.Connection.World;

            var trees = GameObject.Find("Stromy").transform;
            Assert.AreEqual(world.Trees.Length, trees.childCount);
            // Strom z katalogu (ne tvar z kódu s kmenem a korunou), pojmenovaný podle dlaždice a druhu.
            StringAssert.StartsWith("Strom ", trees.GetChild(0).name);
            Assert.IsNull(trees.GetChild(0).Find("Kmen"));
            Assert.IsNotNull(IslandView.FindDeep(GameObject.Find("Rostlina 0").transform, "Listy"));
            Assert.Greater(Object.FindAnyObjectByType<GrassView>().Count, world.Width);

            // Postava a ohně táborů z katalogu.
            yield return WaitFor(() => GameObject.Find("Postava") != null, 5f, "Postava se neobjevila.");
            Assert.IsNotNull(GameObject.Find("Postava").transform.Find(GenerationLook.FigureName + "/Model"));
            if (world.Camps.Length > 0)
            {
                Assert.IsNotNull(IslandView.FindDeep(GameObject.Find("Tábor kmene 1").transform, "Kámen ohniště"));
                Assert.IsNotNull(GameObject.Find("Model plamene"));
            }

            var buildMode = Object.FindAnyObjectByType<BuildMode>();
            buildMode.Open(Oohra.Core.World.BuildKind.StickWall);
            yield return PlaceAround(session, buildMode, () => session.Connection.Structures.Length > 0);
            Assert.AreEqual(1, session.Connection.Structures.Length, "Zeď se nepostavila.");
            yield return new WaitForSecondsRealtime(1f);

            Object.FindAnyObjectByType<CameraRig>().enabled = false;
            var self = session.Connection.SelfSnapshot().Value;
            Camera.main.transform.position = WorldCoords.ToUnity(self.X, self.Y + 6, 5f);
            Camera.main.transform.LookAt(WorldCoords.ToUnity(self.X, self.Y, 0.5f));
            yield return SaveScreenshot("katalog.png");
        }

        /// <summary>Katalog se „modely“ složenými z tvarů: strom (kmen a koruna), rostlina se značenými listy, zeď.</summary>
        private VisualCatalog TestCatalog()
        {
            _templates = new GameObject("Vzory");
            Object.DontDestroyOnLoad(_templates);
            _templates.transform.position = new Vector3(-1000f, 0f, 0f);

            var tree = new GameObject("Vzor stromu");
            tree.transform.SetParent(_templates.transform, false);
            Shape(PrimitiveType.Cylinder, tree.transform, "Kmen", new Vector3(0f, 1.5f, 0f), new Vector3(0.3f, 1.5f, 0.3f), new Color(0.4f, 0.3f, 0.2f));
            Shape(PrimitiveType.Cube, tree.transform, "Koruna", new Vector3(0f, 4f, 0f), new Vector3(2.4f, 2.4f, 2.4f), new Color(0.2f, 0.5f, 0.2f));

            var plant = new GameObject("Vzor rostliny");
            plant.transform.SetParent(_templates.transform, false);
            Shape(PrimitiveType.Cylinder, plant.transform, "Stonek", new Vector3(0f, 0.15f, 0f), new Vector3(0.05f, 0.15f, 0.05f), new Color(0.3f, 0.4f, 0.2f));
            Shape(PrimitiveType.Cube, plant.transform, "Listy", new Vector3(0f, 0.35f, 0f), new Vector3(0.3f, 0.12f, 0.3f), Color.white).AddComponent<SpeciesTint>();

            var wall = new GameObject("Vzor zdi");
            wall.transform.SetParent(_templates.transform, false);
            Shape(PrimitiveType.Cube, wall.transform, "Prkna", new Vector3(0f, 1f, 0f), new Vector3(1f, 2f, 0.1f), new Color(0.6f, 0.45f, 0.3f));

            var catalog = ScriptableObject.CreateInstance<VisualCatalog>();
            catalog.Trees = new[] { tree };
            catalog.Plants = new[] { plant, plant, plant, plant };
            catalog.Wall = wall;

            var grass = new GameObject("Vzor trávy");
            grass.transform.SetParent(_templates.transform, false);
            Shape(PrimitiveType.Cube, grass.transform, "Stébla", new Vector3(5f, 0.5f, 3f), new Vector3(0.3f, 1f, 0.05f), new Color(0.4f, 0.6f, 0.2f));
            catalog.Grass = new[] { grass };

            var player = new GameObject("Vzor postavy");
            player.transform.SetParent(_templates.transform, false);
            Shape(PrimitiveType.Cube, player.transform, "Tělo", new Vector3(0f, 0.6f, 0f), new Vector3(0.45f, 1.2f, 0.3f), Color.white).AddComponent<SpeciesTint>();
            Shape(PrimitiveType.Sphere, player.transform, "Hlava", new Vector3(0f, 1.5f, 0f), Vector3.one * 0.3f, new Color(0.9f, 0.75f, 0.6f));
            catalog.Player = player;

            var hearth = new GameObject("Vzor ohniště");
            hearth.transform.SetParent(_templates.transform, false);
            Shape(PrimitiveType.Cube, hearth.transform, "Kámen ohniště", new Vector3(0f, 0.08f, 0f), new Vector3(0.7f, 0.16f, 0.7f), Color.gray);
            catalog.Campfire = hearth;

            var flame = new GameObject("Vzor plamene");
            flame.transform.SetParent(_templates.transform, false);
            Shape(PrimitiveType.Cube, flame.transform, "Jazyk plamene", new Vector3(0f, 0.3f, 0f), new Vector3(0.2f, 0.5f, 0.2f), new Color(1f, 0.6f, 0.2f));
            catalog.Flame = flame;
            return catalog;
        }

        private static GameObject Shape(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            var shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            var material = new Material(Palette.Lit);
            material.SetColor("_BaseColor", color);
            shape.GetComponent<Renderer>().sharedMaterial = material;
            return shape;
        }
    }
}
