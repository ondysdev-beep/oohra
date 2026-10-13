using System;
using Oohra.Core;

namespace Oohra.Client
{
    /// <summary>
    /// Čas pro zobrazení. Herní minuty a měsíce kalendáře posílá server ve snímku,
    /// tady se z nich jen počítá hodina, den, roční období a světlo (stejné vzorce jako GameClock v Core).
    /// </summary>
    public sealed class ClientClock
    {
        private const double MinutesPerDay = 1440;
        private const int MonthsPerYear = 12;

        public double TotalMinutes { get; set; } = 7 * 60;

        public double CalendarMonths { get; set; }

        public int Day => (int)Math.Floor(TotalMinutes / MinutesPerDay);

        public double Hour => TotalMinutes % MinutesPerDay / 60;

        public int MonthOfYear => (int)Math.Floor(CalendarMonths) % MonthsPerYear;

        public Season Season => (Season)(MonthOfYear / (MonthsPerYear / 4) % 4);

        /// <summary>Světlo 0 (noc) až 1 (den).</summary>
        public double Daylight => Math.Max(0, Math.Min(1, Math.Sin((Hour - 6) / 24 * 2 * Math.PI) * 1.6 + 0.35));
    }
}
