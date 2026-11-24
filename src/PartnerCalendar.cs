using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace VisitorCalendar
{
    internal static class PartnerCalendar
    {
        internal const string ContractsId = "sfk.weeklycontracts", CommissionsId = "gkj.sfk.caravancommissions";
        internal static ReadContract Contract;
        internal static ReadCommission Commission;

        internal static void Bind()
        {
            Contract = BindContract(AssemblyFor(ContractsId));
            Commission = BindCommission(AssemblyFor(CommissionsId));
        }

        internal static ReadContract BindContract(Assembly assembly) =>
            CalendarApiBinding.Bind<ReadContract>(assembly, "WeeklyContracts.CalendarApi", "TryGetActiveDelivery");

        internal static ReadCommission BindCommission(Assembly assembly) =>
            CalendarApiBinding.Bind<ReadCommission>(assembly, "CaravanCommissions.CommissionCalendar", "TryGetPendingDelivery");

        private static Assembly AssemblyFor(string id) =>
            Chainloader.PluginInfos.TryGetValue(id, out var info) && info.Instance != null
                ? info.Instance.GetType().Assembly : null;
    }

    internal static class CalendarApiBinding
    {
        internal static T Bind<T>(Assembly assembly, string type, string method) where T : Delegate
        {
            var api = assembly?.GetType(type);
            if (api == null) return null;
            var signature = typeof(T).GetMethod("Invoke");
            var parameters = Array.ConvertAll(signature.GetParameters(), p => p.ParameterType);
            var query = api.GetMethod(method, BindingFlags.Static | BindingFlags.Public, null, parameters, null);
            return query?.ReturnType == signature.ReturnType
                ? (T)Delegate.CreateDelegate(typeof(T), query, false) : null;
        }
    }
}
