using System;
using System.Collections.Generic;
using SuperFantasyKingdom;

namespace VisitorCalendar
{
    // One snapshot per calendar opening. Both the calendar and HUD use these rules.
    internal sealed class Schedule
    {
        internal readonly Dictionary<Actors, int> FixedDays = new Dictionary<Actors, int>();
        internal readonly int[] Invitations = new int[4];
        internal readonly HashSet<Actors> Known = new HashSet<Actors>();
        internal readonly HashSet<Actors> Arrived = new HashSet<Actors>();
        internal readonly HashSet<Actors> ArrivalOnly = new HashSet<Actors>();
        internal VisitHistory History = new VisitHistory();
        internal bool SharedArrivalOverride;
        internal bool CaravanGateOpened = true;
        internal int ConsumedCaravanDay;
        internal bool PendingMerchantCharter;
        internal bool VanillaCaravanUnlock = true;
        internal int Today, LastDay, HermitInterval, CaravanStart;
        internal bool CaravansUnlocked, Challenge;
        internal bool HermitOfferPossible = true;

        internal List<Actors> ForDay(int day)
        {
            var result = new List<Actors>();
            if (day < 1 || day > LastDay) return result;
            if (History.Days.TryGetValue(day, out var visits))
                foreach (var actor in visits.Keys) Add(actor, observed: true);
            if (day < Today) return result;
            foreach (var entry in FixedDays)
                if (entry.Value == day) Add(entry.Key);

            if (day <= 28 && day % 7 == 0)
            {
                var guest = (Actors)Invitations[day / 7 - 1];
                // The Wizard invitation is stored with the alternate actor ID.
                Add(guest == Actors.WizardUnknown ? Actors.Wizard : guest);
            }

            if (!Challenge && HermitInterval > 0 && day > 2 && (day - 2) % HermitInterval == 0 &&
                (day != Today || HermitOfferPossible))
                Add(Actors.Hermit);
            if (!Challenge && CaravanGateOpened && CaravanStart > 0 && day >= CaravanStart &&
                (VanillaCaravanUnlock ? CaravansUnlocked || day > 14 : day == Today && CaravansUnlocked))
                Add(Actors.Caravan);
            if (day == Today)
                foreach (var actor in Arrived) Add(actor, observed: true);
            return result;

            void Add(Actors actor, bool observed = false)
            {
                if (!observed && (SharedArrivalOverride || ArrivalOnly.Contains(actor) ||
                    (actor == Actors.Caravan && (!CaravanGateOpened || day == ConsumedCaravanDay || CanStillMove(day))))) return;
                if (Known.Contains(actor) && !result.Contains(actor)) result.Add(actor);
            }
        }

        internal string Status(int day, Actors actor, bool claimedToday)
        {
            bool claimed = false;
            bool visited = History.Days.TryGetValue(day, out var visits) && visits.TryGetValue(actor, out claimed);
            if (day == Today && claimedToday) return "Claimed Today";
            if (day < Today && visited && claimed) return "Visited · Claimed";
            if (visited || (day == Today && Arrived.Contains(actor))) return "Visited";
            return "Forecast";
        }

        internal static string CellMarker(bool hasPortrait, int index, int count)
        {
            if (index == 2 && count > 3) return "+" + (count - 2);
            return hasPortrait ? null : "•";
        }

        private bool CanStillMove(int day)
        {
            if (!PendingMerchantCharter || day <= Today + 1) return false;
            // Only the first remaining appointment can move. Once its early day
            // has loaded with the guest slot busy, tomorrow keeps its booking.
            FixedDays.TryGetValue(Actors.Caravan, out int first);
            if (first < Today) first = int.MaxValue;
            for (int i = 0; i < Invitations.Length; i++)
            {
                int booked = (i + 1) * 7;
                if (booked >= Today && Invitations[i] == (int)Actors.Caravan)
                    first = Math.Min(first, booked);
            }
            return day == first;
        }
    }
}
