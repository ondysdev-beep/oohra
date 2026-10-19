using System.Collections.Generic;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>Kde a jak by stála zeď po přichycení: střed na mapě, úhel ve stupních a délka v metrech.</summary>
    public struct SnapResult
    {
        public Vector2 Center;
        public float AngleDegrees;
        public float Length;

        /// <summary>Zeď je natažená mezi dva konce jiných zdí (dorovnává mezeru).</summary>
        public bool Fits;
    }

    /// <summary>
    /// Modulární stavba: nová zeď se jedním koncem přichytí ke konci jiné zdi (nebo zdi s otvorem) blízko kurzoru.
    /// Možnosti jsou čtyři směry od toho konce (rovně a do stran po 90°, pootočené o úhel z Q a E)
    /// a „dorovnání“: zeď natažená přesně k jinému konci zdi do 1 m, aby se mezera zavřela a zdi do sebe zapadly.
    /// Vybere se možnost, jejíž střed je nejblíž kurzoru (dorovnání má mírnou přednost).
    /// </summary>
    public static class ModularSnap
    {
        public const float SnapRadius = 0.8f;

        // Nejkratší zeď (jako minLength v building.json).
        public const float MinLength = 0.25f;

        // O kolik se dorovnání upřednostní před obyčejnými směry (násobek vzdálenosti od kurzoru).
        private const float FitPreference = 0.7f;

        private struct End
        {
            public Vector2 Point;
            public Vector2 WallCenter;
            public float WallAngle;
            public int WallId;
        }

        /// <summary>
        /// structures: stavby na ostrově, pointer: kurzor na mapě, length: délka vybrané zdi,
        /// turnDegrees: pootočení z Q a E (bere se jen část do 90°, hrubý směr určí kurzor),
        /// canFit: smí se dorovnávat (jen zeď, zeď s otvorem má pevnou délku).
        /// </summary>
        /// <summary>Jak daleko od kurzoru se hledá sousední střecha nebo podlaha (metry).</summary>
        public const float PanelSearch = 3.5f;

        /// <summary>
        /// Střecha nebo podlaha (čtverec size): přichytí se těsně vedle stejné stavby blízko kurzoru (jedna ze čtyř
        /// stran, stejný úhel). Když žádná není, natočí se podle nejbližší zdi (střed zůstane u kurzoru).
        /// Vrací false, když není k čemu se přichytit.
        /// </summary>
        public static bool Panel(IList<StructureView> structures, Vector2 pointer, BuildKind kind, float size, out SnapResult result)
        {
            result = new SnapResult { Center = pointer, Length = size };
            var best = float.MaxValue;
            foreach (var s in structures)
            {
                var center = new Vector2(s.X, s.Y);
                if (s.Kind != kind || Vector2.Distance(center, pointer) > PanelSearch)
                {
                    continue;
                }

                var along = new Vector2(Mathf.Cos(s.Angle), Mathf.Sin(s.Angle)) * size;
                var across = new Vector2(-along.y, along.x);
                foreach (var spot in new[] { center + along, center - along, center + across, center - across })
                {
                    var distance = Vector2.Distance(spot, pointer);
                    if (distance < best && !Occupied(structures, kind, spot, size))
                    {
                        best = distance;
                        result.Center = spot;
                        result.AngleDegrees = s.Angle * Mathf.Rad2Deg;
                    }
                }
            }

            if (best < float.MaxValue)
            {
                return true;
            }

            // První střecha: natočit podle nejbližší zdi.
            var wallDistance = PanelSearch;
            var found = false;
            foreach (var s in structures)
            {
                var distance = Vector2.Distance(new Vector2(s.X, s.Y), pointer);
                if ((s.Kind == BuildKind.StickWall || s.Kind == BuildKind.DoorFrame) && distance < wallDistance)
                {
                    wallDistance = distance;
                    result.AngleDegrees = s.Angle * Mathf.Rad2Deg;
                    found = true;
                }
            }

            return found;
        }

        private static bool Occupied(IList<StructureView> structures, BuildKind kind, Vector2 spot, float size)
        {
            foreach (var s in structures)
            {
                if (s.Kind == kind && Vector2.Distance(new Vector2(s.X, s.Y), spot) < size * 0.95f)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool Find(IList<StructureView> structures, Vector2 pointer, float length, float turnDegrees, bool canFit, out SnapResult result)
        {
            result = default(SnapResult);
            var ends = EndsOf(structures);
            var anchorIndex = -1;
            var best = SnapRadius;
            for (var i = 0; i < ends.Count; i++)
            {
                var distance = Vector2.Distance(ends[i].Point, pointer);
                if (distance < best)
                {
                    best = distance;
                    anchorIndex = i;
                }
            }

            if (anchorIndex < 0)
            {
                return false;
            }

            var anchor = ends[anchorIndex];
            var bestScore = float.MaxValue;
            var turn = Mathf.Repeat(turnDegrees, 90f);
            for (var k = 0; k < 4; k++)
            {
                var angle = anchor.WallAngle + k * 90f + turn;
                var direction = Direction(angle);

                // Zpátky do vlastní zdi ne.
                if (Vector2.Dot(direction, (anchor.WallCenter - anchor.Point).normalized) > 0.9f)
                {
                    continue;
                }

                Consider(anchor.Point + direction * (length / 2), angle, length, false, pointer, ref bestScore, ref result);
            }

            if (canFit)
            {
                foreach (var other in ends)
                {
                    var gap = other.Point - anchor.Point;
                    if (other.WallId == anchor.WallId || gap.magnitude < MinLength || gap.magnitude > StructureViews.WallLength + 0.001f)
                    {
                        continue;
                    }

                    var angle = Mathf.Atan2(gap.y, gap.x) * Mathf.Rad2Deg;
                    Consider(anchor.Point + gap / 2, angle, gap.magnitude, true, pointer, ref bestScore, ref result);
                }
            }

            return true;
        }

        private static void Consider(Vector2 center, float angle, float length, bool fits, Vector2 pointer, ref float bestScore, ref SnapResult result)
        {
            var score = Vector2.Distance(center, pointer) * (fits ? FitPreference : 1f);
            if (score < bestScore)
            {
                bestScore = score;
                result = new SnapResult { Center = center, AngleDegrees = angle, Length = length, Fits = fits };
            }
        }

        /// <summary>Konce všech zdí a zdí s otvorem (dveře mají stejné místo jako jejich zeď, ty se přeskočí).</summary>
        private static List<End> EndsOf(IList<StructureView> structures)
        {
            var ends = new List<End>();
            foreach (var s in structures)
            {
                if (s.Kind != BuildKind.StickWall && s.Kind != BuildKind.DoorFrame)
                {
                    continue;
                }

                var center = new Vector2(s.X, s.Y);
                var angle = s.Angle * Mathf.Rad2Deg;
                var half = Direction(angle) * (s.Length / 2);
                ends.Add(new End { Point = center - half, WallCenter = center, WallAngle = angle, WallId = s.Id });
                ends.Add(new End { Point = center + half, WallCenter = center, WallAngle = angle, WallId = s.Id });
            }

            return ends;
        }

        private static Vector2 Direction(float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }
}
