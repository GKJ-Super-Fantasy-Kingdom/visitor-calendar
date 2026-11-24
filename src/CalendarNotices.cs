using System;
using System.Collections.Generic;
using SuperFantasyKingdom;

namespace VisitorCalendar
{
    internal delegate bool ReadContract(out string title, out int quantity, out string resource, out int deadline);
    internal delegate bool ReadCommission(out int visitDay);

    // Orders are a snapshot of the open calendar. They never become visitor history.
    internal sealed class CalendarNotice
    {
        internal readonly int? Day;
        internal readonly string Title, Detail;
        internal readonly bool IsCommission;

        private CalendarNotice(int? day, string title, string detail, bool commission = false)
        {
            Day = day;
            Title = title;
            Detail = detail;
            IsCommission = commission;
        }

        internal static List<CalendarNotice> Read(Schedule schedule, ReadContract contract, ReadCommission commission,
            Action<Exception> warn)
        {
            var notices = new List<CalendarNotice>(2);
            try
            {
                if (contract != null && contract(out var title, out int quantity, out var resource, out int deadline) &&
                    deadline >= schedule.Today && deadline <= schedule.LastDay)
                    notices.Add(new CalendarNotice(deadline, "Delivery Due", $"{title} · {quantity} {resource}"));
            }
            catch (Exception e) { warn(e); }
            try
            {
                if (commission != null && commission(out int confirmedDay))
                {
                    // Only the provider can distinguish today's open item offer from a spent visit.
                    if (confirmedDay == schedule.Today)
                        notices.Add(new CalendarNotice(confirmedDay, "Commission Delivery", "Prepaid item commission", commission: true));
                    else
                    {
                        int? day = NextBooking(schedule);
                        notices.Add(day.HasValue
                            ? new CalendarNotice(day, "Commission Forecast", "Prepaid order · Next scheduled item caravan", commission: true)
                            : new CalendarNotice(null, "Next Caravan", "Prepaid commission · Date unknown", commission: true));
                    }
                }
            }
            catch (Exception e) { warn(e); }
            return notices;
        }

        private static int? NextBooking(Schedule schedule)
        {
            for (int day = schedule.Today + 1; day <= schedule.LastDay; ++day)
                if (schedule.ForDay(day).Contains(Actors.Caravan)) return day;
            return null;
        }
    }
}
