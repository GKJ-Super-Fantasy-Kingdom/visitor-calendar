using System.Collections.Generic;
using System.Globalization;
using SuperFantasyKingdom;

namespace VisitorCalendar
{
    internal sealed class VisitHistory
    {
        internal readonly Dictionary<int, Dictionary<Actors, bool>> Days =
            new Dictionary<int, Dictionary<Actors, bool>>();

        internal void Record(int day, Actors actor, bool claimed)
        {
            if (day < 1 || actor == Actors.None) return;
            if (!Days.TryGetValue(day, out var visits))
                Days.Add(day, visits = new Dictionary<Actors, bool>());
            if (!visits.ContainsKey(actor) || claimed) visits[actor] = claimed;
        }

        internal void RecordCaravan(int day, CaravanDecision decision)
        {
            if (decision == CaravanDecision.Claimed || decision == CaravanDecision.Declined)
                Record(day, Actors.Caravan, decision == CaravanDecision.Claimed);
        }

        internal IEnumerable<string> Lines()
        {
            foreach (var day in Days)
                foreach (var visit in day.Value)
                    yield return string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}",
                        day.Key, (int)visit.Key, visit.Value ? 1 : 0);
        }

        internal void Restore(IEnumerable<string> lines, int lastDay)
        {
            foreach (string line in lines)
            {
                var fields = line.Split('|');
                if (fields.Length == 3 && int.TryParse(fields[0], out int day) && day <= lastDay &&
                    int.TryParse(fields[1], out int id) && (fields[2] == "1" || fields[2] == "0"))
                    Record(day, (Actors)id, fields[2] == "1");
            }
        }
    }
}
