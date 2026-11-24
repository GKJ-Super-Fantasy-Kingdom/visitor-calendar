using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SuperFantasyKingdom;

namespace VisitorCalendar
{
    // Observe the result the game actually used. Calling a mod's scheduler to
    // probe hypothetical days can advance its RNG or reserve a guest slot.
    [HarmonyPatch]
    internal static class NativeBookings
    {
        private static readonly Dictionary<Type, Actors> Guests = new Dictionary<Type, Actors>
        {
            { typeof(GuestHermit), Actors.Hermit }, { typeof(GuestMerchant), Actors.Merchant },
            { typeof(GuestMonsterHunter), Actors.MonsterHunter }, { typeof(GuestWitchDoctor), Actors.WitchDoctor },
            { typeof(GuestGnomes), Actors.Gnomes }, { typeof(GuestBuilder), Actors.Builder },
            { typeof(GuestDemolisher), Actors.Demolisher }, { typeof(GuestCaravan), Actors.Caravan },
            { typeof(GuestWizard), Actors.Wizard }, { typeof(GuestResearcher), Actors.Researcher }
        };
        private static readonly Dictionary<Actors, int> effectiveDays = new Dictionary<Actors, int>();
        private static readonly HashSet<Actors> patchedDates = new HashSet<Actors>();
        internal static readonly HashSet<Actors> ArrivalOnly = new HashSet<Actors>();
        internal static bool SharedArrivalOverride;
        internal static bool CaravanUnlockPatched;

        private static IEnumerable<MethodBase> TargetMethods() =>
            Guests.Keys.Select(type => AccessTools.DeclaredMethod(type, "GetDay"));

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Guest __instance, int __result)
        {
            if (Guests.TryGetValue(__instance.GetType(), out var actor)) effectiveDays[actor] = __result;
        }

        internal static void BeginLoad()
        {
            effectiveDays.Clear();
            patchedDates.Clear();
            ArrivalOnly.Clear();
            SharedArrivalOverride = Changed(typeof(Guest), "OnLoad") || Changed(typeof(WorldManager), "IsTodayGuest");
            CaravanUnlockPatched = Changed(typeof(WorldManager), "HasUnlockedCaravans");
            foreach (var entry in Guests)
            {
                if (Changed(entry.Key, "GetDay")) patchedDates.Add(entry.Value);
                if (Changed(entry.Key, "CheckDay", entry.Value) || Changed(entry.Key, "OnLoad", entry.Value))
                    ArrivalOnly.Add(entry.Value);
            }
        }

        private static bool Changed(Type type, string name, Actors actor = Actors.None)
        {
            var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var patches = method == null ? null : Harmony.GetPatchInfo(method);
            if (patches == null) return false;
            return patches.Prefixes.Concat(patches.Postfixes).Concat(patches.Transpilers)
                .Concat(patches.Finalizers).Concat(patches.ILManipulators)
                .Any(patch => patch.owner != Plugin.Id &&
                    !(actor == Actors.Hermit && patch.owner == Visitors.Weekly) &&
                    !(actor == Actors.Caravan && (patch.owner == Visitors.Daily || patch.owner == Visitors.Charters)) &&
                    !CommissionObserver(type, name, patch.owner, patch.PatchMethod));
        }

        internal static bool CommissionObserver(Type type, string target, string owner, MethodInfo hook)
        {
            if (owner != PartnerCalendar.CommissionsId || target != "OnLoad") return false;
            // These hooks identify a distinct visit and unwind the dispatch scope.
            // None replaces the game's scheduling decision.
            string declaring = hook.DeclaringType?.FullName;
            return (type == typeof(Guest) && declaring == "CaravanCommissions.Plugin+Arrival" && hook.Name == "Prefix") ||
                (type == typeof(GuestCaravan) && declaring == "CaravanCommissions.Plugin+ScheduledArrival" &&
                 (hook.Name == "Prefix" || hook.Name == "Finalizer"));
        }

        internal static int FixedDay(Actors actor, int authored)
        {
            if (effectiveDays.TryGetValue(actor, out int day)) return day;
            return patchedDates.Contains(actor) ? 0 : authored;
        }
    }
}
