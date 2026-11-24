using System.Collections.Generic;
using SuperFantasyKingdom;

namespace VisitorCalendar
{
    internal struct CardResolution
    {
        internal Actors Actor;
        internal bool Confirmed;

        internal void Finish(HashSet<Actors> claims, bool completed)
        {
            if (completed && Confirmed && Actor != Actors.None) claims.Add(Actor);
        }
    }
}
