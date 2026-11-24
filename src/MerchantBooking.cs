using System.Reflection;

namespace VisitorCalendar
{
    internal static class MerchantBooking
    {
        // Optional read of the charter's persisted booking, scoped to this run.
        // An absent/incompatible record supplies no evidence of a moved date.
        internal static int? FirstVisit(object plugin, string runId, bool challenge, bool dailyCaravan)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            if (challenge || dailyCaravan || plugin == null || string.IsNullOrEmpty(runId)) return null;
            var type = plugin.GetType();
            if (!Equals(type.GetField("RunId", flags)?.GetValue(plugin), runId)) return null;
            var state = type.GetField("Run", flags)?.GetValue(plugin);
            if (state == null || state.GetType().GetField("choice", flags)?.GetValue(state)?.ToString() != "Merchant")
                return null;
            return state.GetType().GetField("firstVisit", flags)?.GetValue(state) as int?;
        }
    }
}
