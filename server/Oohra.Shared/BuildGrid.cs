using System;
using System.Collections.Generic;

namespace Oohra.Core.World
{
    /// <summary>
    /// Plán stavby: počátek a natočení stavební mřížky (políčka 2 × 2 m). Založí ho první díl stavby, další díly
    /// poblíž se k němu přichytí, takže nic není křivé.
    /// </summary>
    public readonly struct GridPlan
    {
        public GridPlan(int id, double x, double y, double angle)
        {
            Id = id;
            X = x;
            Y = y;
            Angle = angle;
        }

        public int Id { get; }

        public double X { get; }

        public double Y { get; }

        /// <summary>Natočení mřížky v radiánech (0 = osa u míří na východ, y mapy roste na jih).</summary>
        public double Angle { get; }
    }

    /// <summary>Díl stavby, který patří k plánu (číslo plánu a střed dílu na mapě).</summary>
    public readonly struct GridPiece
    {
        public GridPiece(int planId, double x, double y)
        {
            PlanId = planId;
            X = x;
            Y = y;
        }

        public int PlanId { get; }

        public double X { get; }

        public double Y { get; }
    }

    /// <summary>Kam se díl přichytil: střed, úhel a délka, k jakému plánu (-1 = založí nový plán s počátkem PlanX, PlanY a úhlem PlanAngle).</summary>
    public readonly struct GridSnap
    {
        public GridSnap(int planId, double x, double y, double angle, double length, double planX, double planY, double planAngle)
        {
            PlanId = planId;
            X = x;
            Y = y;
            Angle = angle;
            Length = length;
            PlanX = planX;
            PlanY = planY;
            PlanAngle = planAngle;
        }

        public int PlanId { get; }

        public double X { get; }

        public double Y { get; }

        public double Angle { get; }

        public double Length { get; }

        public double PlanX { get; }

        public double PlanY { get; }

        public double PlanAngle { get; }

        public bool NewPlan => PlanId < 0;
    }

    /// <summary>
    /// Stavební mřížka (políčka Cell × Cell metrů). Počítá ji stejně server (rozhodne, kde díl stojí) i klient
    /// (náhled), proto je ve sdílené knihovně. Podlaha a střecha zaberou políčko, zeď a zeď s otvorem hranu
    /// políčka (celou, Cell metrů), sloupek roh. Díl do SnapDistance od dílu plánu se k plánu přichytí, jinak
    /// založí nový plán: natočení podle hráče (po AngleStep stupních), díl leží přesně pod kurzorem.
    /// </summary>
    public static class BuildGrid
    {
        public const double Cell = 2.0;

        /// <summary>Jak blízko (metry) od dílu plánu se nový díl k plánu přichytí.</summary>
        public const double SnapDistance = 3.0;

        /// <summary>Po kolika stupních se natáčí nový plán (Q a E).</summary>
        public const double AngleStep = 15.0;

        /// <summary>Které stavby stojí v mřížce. Dveře jdou do otvoru, oheň, past, koš, dekorace a žebřík stojí volně.</summary>
        public static bool OnGrid(BuildKind kind)
        {
            return kind == BuildKind.StickWall || kind == BuildKind.DoorFrame || kind == BuildKind.Floor || kind == BuildKind.Roof || kind == BuildKind.Post;
        }

        /// <summary>Nejbližší plán, ke kterému se díl v (x, y) přichytí, nebo null.</summary>
        public static GridPlan? NearestPlan(IReadOnlyList<GridPlan> plans, IEnumerable<GridPiece> pieces, double x, double y)
        {
            var bestId = -1;
            var best = SnapDistance;
            foreach (var piece in pieces)
            {
                var d = Math.Sqrt((piece.X - x) * (piece.X - x) + (piece.Y - y) * (piece.Y - y));
                if (d <= best)
                {
                    best = d;
                    bestId = piece.PlanId;
                }
            }

            foreach (var plan in plans)
            {
                if (plan.Id == bestId)
                {
                    return plan;
                }
            }

            return null;
        }

        /// <summary>Přichytí díl s kurzorem v (x, y) a úhlem hráče angle (radiány) k mřížce.</summary>
        public static GridSnap Snap(IReadOnlyList<GridPlan> plans, IEnumerable<GridPiece> pieces, BuildKind kind, double x, double y, double angle)
        {
            var near = NearestPlan(plans, pieces, x, y);
            return near.HasValue ? OnPlan(near.Value, kind, x, y, angle) : NewPlan(kind, x, y, angle);
        }

        /// <summary>Díl na existujícím plánu: políčko, hrana nebo roh pod kurzorem. U zdi úhel hráče vybere směr hrany.</summary>
        public static GridSnap OnPlan(GridPlan plan, BuildKind kind, double x, double y, double angle)
        {
            var (u, v) = ToLocal(plan, x, y);
            double cu, cv, pieceAngle = plan.Angle, length = Cell;
            switch (kind)
            {
                case BuildKind.Post:
                    (cu, cv) = (Math.Round(u), Math.Round(v));
                    length = 0.01;
                    pieceAngle = 0;
                    break;
                case BuildKind.StickWall:
                case BuildKind.DoorFrame:
                    if (AlongU(angle - plan.Angle))
                    {
                        (cu, cv) = (Math.Floor(u) + 0.5, Math.Round(v));
                    }
                    else
                    {
                        (cu, cv) = (Math.Round(u), Math.Floor(v) + 0.5);
                        pieceAngle = plan.Angle + Math.PI / 2;
                    }

                    break;
                default:
                    (cu, cv) = (Math.Floor(u) + 0.5, Math.Floor(v) + 0.5);
                    break;
            }

            var (wx, wy) = ToWorld(plan, cu, cv);
            return new GridSnap(plan.Id, wx, wy, Normalize(pieceAngle), length, plan.X, plan.Y, plan.Angle);
        }

        /// <summary>První díl: založí plán natočený podle hráče (po AngleStep), díl leží přesně pod kurzorem.</summary>
        public static GridSnap NewPlan(BuildKind kind, double x, double y, double angle)
        {
            var step = AngleStep * Math.PI / 180;
            var a = Normalize(Math.Round(angle / step) * step);
            var (cu, cv) = kind switch
            {
                BuildKind.Post => (0.0, 0.0),
                BuildKind.StickWall or BuildKind.DoorFrame => (0.5, 0.0),
                _ => (0.5, 0.5),
            };

            // Počátek mřížky tak, aby díl ležel na kurzoru.
            var (ox, oy) = (x - (Math.Cos(a) * cu - Math.Sin(a) * cv) * Cell, y - (Math.Sin(a) * cu + Math.Cos(a) * cv) * Cell);
            var length = kind == BuildKind.Post ? 0.01 : Cell;
            return new GridSnap(-1, x, y, kind == BuildKind.Post ? 0 : a, length, ox, oy, a);
        }

        /// <summary>Souřadnice v mřížce plánu (v políčkách) z bodu na mapě.</summary>
        public static (double U, double V) ToLocal(GridPlan plan, double x, double y)
        {
            var (dx, dy) = (x - plan.X, y - plan.Y);
            var (c, s) = (Math.Cos(plan.Angle), Math.Sin(plan.Angle));
            return ((dx * c + dy * s) / Cell, (-dx * s + dy * c) / Cell);
        }

        /// <summary>Bod na mapě ze souřadnic v mřížce plánu (v políčkách).</summary>
        public static (double X, double Y) ToWorld(GridPlan plan, double u, double v)
        {
            var (c, s) = (Math.Cos(plan.Angle), Math.Sin(plan.Angle));
            return (plan.X + (u * c - v * s) * Cell, plan.Y + (u * s + v * c) * Cell);
        }

        /// <summary>Úhel (vůči plánu) je blíž ose u než ose v (zeď vede podél u).</summary>
        private static bool AlongU(double relative)
        {
            var r = Math.Abs(Math.IEEERemainder(relative, Math.PI));
            return r < Math.PI / 4;
        }

        private static double Normalize(double angle)
        {
            var a = Math.IEEERemainder(angle, 2 * Math.PI);
            return a < 0 ? a + 2 * Math.PI : a;
        }
    }
}
