using System.Collections.Generic;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;
using UnityEngine.Rendering;

namespace Oohra.Client
{
    /// <summary>
    /// Stopy v bahně podle serveru: tmavé otisky na zemi natočené ve směru chůze. Člověk šlapku, zvířata menší
    /// otisky podle velikosti, had vlnitou čáru. Čerstvé stopy jsou tmavé, staré blednou. Jedna síť pro všechny
    /// stopy v okolí, postaví se znovu, když server pošle nový seznam.
    /// </summary>
    public class TrackViews : MonoBehaviour
    {
        /// <summary>Výška otisku nad zemí (nad zatopenou souší, ať je vidět i pod kalnou vodou).</summary>
        public const float Height = 0.04f;

        private static readonly Color Mud = new Color(0.16f, 0.12f, 0.08f, 0.8f);

        private GameConnection _connection;
        private TrackView[] _shown;
        private MeshFilter _filter;

        public void Init(GameConnection connection)
        {
            _connection = connection;
            _filter = gameObject.AddComponent<MeshFilter>();
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var material = new Material(Palette.Foam) { name = "Stopy" };
            material.SetColor("_BaseColor", Color.white);
            renderer.sharedMaterial = material;
        }

        /// <summary>Kolik stop je vidět (pro testy).</summary>
        public int Count { get; private set; }

        private void Update()
        {
            if (_connection == null || ReferenceEquals(_connection.Tracks, _shown))
            {
                return;
            }

            _shown = _connection.Tracks;
            Count = _shown.Length;
            if (_filter.sharedMesh != null)
            {
                Destroy(_filter.sharedMesh);
            }

            _filter.sharedMesh = BuildMesh(_shown);
        }

        /// <summary>Velikost otisku (šířka a délka v metrech) podle toho, kdo stopu udělal.</summary>
        public static Vector2 PrintSize(TrackMaker maker)
        {
            switch (maker)
            {
                case TrackMaker.Human: return new Vector2(0.11f, 0.26f);
                case TrackMaker.Boar: return new Vector2(0.09f, 0.1f);
                case TrackMaker.Deer: return new Vector2(0.07f, 0.11f);
                case TrackMaker.Wolf: return new Vector2(0.1f, 0.1f);
                case TrackMaker.Rabbit: return new Vector2(0.05f, 0.08f);
                default: return new Vector2(0.05f, 0.8f);
            }
        }

        public static Mesh BuildMesh(TrackView[] tracks)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            for (var i = 0; i < tracks.Length; i++)
            {
                var t = tracks[i];
                var angle = t.Dir / 256f * Mathf.PI * 2f;
                // Směr na mapě (y dolů) v Unity (z nahoru), vodorovně.
                var forward = new Vector3(Mathf.Cos(angle), 0f, -Mathf.Sin(angle));
                var side = new Vector3(-forward.z, 0f, forward.x);
                var center = WorldCoords.ToUnity(t.X, t.Y, Height);
                var color = new Color(Mud.r, Mud.g, Mud.b, Mud.a * t.Fresh / 255f);
                var size = PrintSize(t.Maker);
                if (t.Maker == TrackMaker.Snake)
                {
                    // Had: dva kusy čáry trochu do stran, vypadá to jako vlnka.
                    AddPrint(center + side * 0.04f - forward * 0.2f, forward, side, size.x, size.y / 2, color, vertices, colors, triangles);
                    AddPrint(center - side * 0.04f + forward * 0.2f, forward, side, size.x, size.y / 2, color, vertices, colors, triangles);
                    continue;
                }

                // Levá a pravá noha se střídají: stopa se posune trochu do strany podle polohy.
                var foot = (Mathf.FloorToInt(t.X * 7f + t.Y * 13f) & 1) == 0 ? 1f : -1f;
                var spread = t.Maker == TrackMaker.Human ? 0.12f : 0.08f;
                AddPrint(center + side * (foot * spread), forward, side, size.x, size.y, color, vertices, colors, triangles);
                if (t.Maker != TrackMaker.Human)
                {
                    // Zvíře na čtyřech: druhý otisk vedle.
                    AddPrint(center - side * (foot * spread) + forward * 0.15f, forward, side, size.x, size.y, color, vertices, colors, triangles);
                }
            }

            var mesh = new Mesh { name = "Stopy", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            // Otisky leží na zemi: normála nahoru. (Výpočet z oboustranné sítě by dal nulovou normálu a světlo
            // by pak vracelo neplatné hodnoty, záře z nich udělala velké bílé skvrny.)
            var normals = new Vector3[vertices.Count];
            for (var n = 0; n < normals.Length; n++) normals[n] = Vector3.up;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddPrint(Vector3 center, Vector3 forward, Vector3 side, float width, float length, Color color, List<Vector3> vertices, List<Color> colors, List<int> triangles)
        {
            var start = vertices.Count;
            var f = forward * (length / 2);
            var s = side * (width / 2);
            vertices.Add(center - f - s);
            vertices.Add(center - f + s);
            vertices.Add(center + f + s);
            vertices.Add(center + f - s);
            for (var k = 0; k < 4; k++)
            {
                colors.Add(color);
            }

            // Materiál stop (jako pěna) kreslí obě strany, stačí jedna sada trojúhelníků.
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
    }
}
