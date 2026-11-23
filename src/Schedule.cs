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
        internal int Today, LastDay, HermitInterval, CaravanStart;
        internal bool CaravansUnlocked, Challenge;
        internal bool HermitOfferPossible = true;

        internal List<Actors> ForDay(int day)
        {
            var result = new List<Actors>();
            if (day < 1 || day > LastDay) return result;
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
            if (!Challenge && CaravanStart > 0 && day >= CaravanStart && (CaravansUnlocked || day > 14))
                Add(Actors.Caravan);
            if (day == Today)
                foreach (var actor in Arrived) Add(actor);
            return result;

            void Add(Actors actor)
            {
                if (Known.Contains(actor) && !result.Contains(actor)) result.Add(actor);
            }
        }
    }
}
