using System.Collections.Generic;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;
using UnityEngine.Rendering;

namespace Oohra.Client
{
    /// <summary>
    /// Stěny jeskyní: skála (CaveWall), která sousedí s podlahou, je tmavý blok vysoký WallHeight. Bloky jsou
    /// jedna síť (vršek a strany do jeskyně). Hlubší skála se nekreslí, v jeskyni je tma a černá mlha.
    /// Barva je v katalogu vzhledu (CaveWallColor).
    /// </summary>
    public static class CaveView
    {
        /// <summary>Výška stěny jeskyně (nízká, ať nezakrývá postavu při pohledu shora).</summary>
        public const float WallHeight = 1.6f;

        private static readonly int[] _dx = { 1, -1, 0, 0 };
        private static readonly int[] _dy = { 0, 0, 1, -1 };

        public static GameObject Build(WorldView world, Transform parent)
        {
            var go = new GameObject("Jeskyně");
            go.transform.SetParent(parent);
            var mesh = BuildMesh(world);
            if (mesh.vertexCount == 0)
            {
                return go;
            }

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Palette.Lit;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            Palette.SetColor(go, VisualCatalog.Current.CaveWallColor);
            return go;
        }

        /// <summary>Místo (x, y) na mapě je v jeskyni: na podlaze jeskyně nebo vedle skály kolem ní.</summary>
        public static bool IsCave(WorldView world, double x, double y)
        {
            var (tx, ty) = ((int)System.Math.Floor(x), (int)System.Math.Floor(y));
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var (nx, ny) = (tx + dx, ty + dy);
                    if (nx < 0 || ny < 0 || nx >= world.Width || ny >= world.Height)
                    {
                        continue;
                    }

                    var tile = world.Tiles[ny * world.Width + nx];
                    if (tile == TileType.CaveFloor || tile == TileType.CaveWall)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static Mesh BuildMesh(WorldView world)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var y = 0; y < world.Height; y++)
            {
                for (var x = 0; x < world.Width; x++)
                {
                    if (world.Tiles[y * world.Width + x] != TileType.CaveWall)
                    {
                        continue;
                    }

                    var open = new bool[4];
                    var any = false;
                    for (var k = 0; k < 4; k++)
                    {
                        open[k] = IsOpen(world, x + _dx[k], y + _dy[k]);
                        any |= open[k];
                    }

                    if (any)
                    {
                        AddBlock(x, y, open, vertices, triangles);
                    }
                }
            }

            var mesh = new Mesh { name = "Stěny jeskyní", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Do jeskyně je otevřeno: podlaha, kaluž nebo pazourek (všechno, co není skála).</summary>
        private static bool IsOpen(WorldView world, int x, int y)
        {
            return x >= 0 && y >= 0 && x < world.Width && y < world.Height && world.Tiles[y * world.Width + x] != TileType.CaveWall
                && world.Tiles[y * world.Width + x] != TileType.Sea;
        }

        /// <summary>Blok: vršek a strany, které vedou do jeskyně (ostatní nejsou vidět).</summary>
        private static void AddBlock(int x, int y, bool[] open, List<Vector3> vertices, List<int> triangles)
        {
            var h = WallHeight;
            // Rohy na mapě (x doprava, y dolů), v Unity je osa y mapy -z.
            var a = WorldCoords.ToUnity(x, y, h);
            var b = WorldCoords.ToUnity(x + 1, y, h);
            var c = WorldCoords.ToUnity(x + 1, y + 1, h);
            var d = WorldCoords.ToUnity(x, y + 1, h);
            Quad(a, b, c, d, Vector3.up, vertices, triangles);
            var down = Vector3.down * h;
            // Strany: open[0] je +x mapy, open[1] -x, open[2] +y mapy (v Unity -z), open[3] -y (v Unity +z).
            if (open[0]) Quad(b + down, c + down, c, b, Vector3.right, vertices, triangles);
            if (open[1]) Quad(d + down, a + down, a, d, Vector3.left, vertices, triangles);
            if (open[2]) Quad(c + down, d + down, d, c, Vector3.back, vertices, triangles);
            if (open[3]) Quad(a + down, b + down, b, a, Vector3.forward, vertices, triangles);
        }

        /// <summary>Čtyřúhelník ze dvou trojúhelníků, lícem ve směru facing (pořadí vrcholů se podle toho otočí).</summary>
        private static void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 facing, List<Vector3> vertices, List<int> triangles)
        {
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), facing) < 0f)
            {
                var swap = p1;
                p1 = p3;
                p3 = swap;
            }

            var start = vertices.Count;
            vertices.Add(p0);
            vertices.Add(p1);
            vertices.Add(p2);
            vertices.Add(p3);
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
    }
}
