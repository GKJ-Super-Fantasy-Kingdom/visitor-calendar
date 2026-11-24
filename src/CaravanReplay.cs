namespace VisitorCalendar
{
    internal enum CaravanDecision { Unavailable, Pending, Declined, Claimed }

    internal static class CaravanReplay
    {
        // An assigned kingdom can also be a queued invitation. Only a completed
        // decision proves a visit when Daily Caravan suppresses a replay.
        internal static CaravanDecision Read(object visit)
        {
            if (visit == null) return CaravanDecision.Unavailable;
            var type = visit.GetType();
            if (!(type.GetField("kingdom")?.GetValue(visit) is int kingdom) || kingdom < 0)
                return CaravanDecision.Unavailable;
            var selected = type.GetField("selected");
            if (selected?.FieldType != typeof(string)) return CaravanDecision.Unavailable;
            if (!string.IsNullOrEmpty((string)selected.GetValue(visit))) return CaravanDecision.Claimed;
            return Equals(type.GetField("declined")?.GetValue(visit), true)
                ? CaravanDecision.Declined : CaravanDecision.Pending;
        }
    }
}
