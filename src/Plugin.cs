using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VisitorCalendar
{
    [BepInPlugin(Id, "Visitor Calendar", "0.1.2")]
    [BepInDependency(Visitors.Weekly, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(Visitors.Daily, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(Visitors.Charters, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal const string Id = "sfk.visitorcalendar";
        private static ManualLogSource log;
        private Harmony patches;
        private void Start() => PartnerCalendar.Bind();
        private void Awake()
        {
            log = Logger;
            patches = new Harmony(Id);
            try { patches.PatchAll(typeof(Plugin).Assembly); }
            catch { patches.UnpatchSelf(); throw; }
            WorldManager.OnGuestArriving += VisitState.Arrival;
            Logger.LogInfo("Visitor Calendar 0.1.2 ready.");
        }
        private void OnDestroy()
        {
            WorldManager.OnGuestArriving -= VisitState.Arrival;
            patches?.UnpatchSelf();
            RestoreMinimizedControls();
        }

        // The native calendar dereferences an invited actor while binding, before
        // our view exists. An unregistered ID must not prevent opening the grid.
        [HarmonyPatch(typeof(UICalendar), "GetGuestImage")]
        private static class MissingActor
        {
            private static bool Prefix(int guestName) => Visitors.Actor((Actors)guestName) != null;
        }

        [HarmonyPatch(typeof(UICalendar), "GetGuestTitle")]
        private static class MissingTitle
        {
            private static bool Prefix(int guestName, ref string __result)
            {
                if (guestName == 0) return true;
                var id = (Actors)guestName;
                var title = Visitors.Actor(id)?.stringTitle;
                if (title != null && !title.IsEmpty) return true;
                __result = Visitors.Title(id);
                return false;
            }
        }
        internal static void LogWarning(Exception e) => log.LogWarning(e.Message);

        internal static void Focus(GameObject target)
        {
            if (target != null && !target.activeInHierarchy) return;
            GamepadManager.Instance.SetGameObjectToSelect(target, force: true);
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != target) events.SetSelectedGameObject(target);
        }

        private static GameObject returnTo;
        private static OverlayManager minimizedOwner;
        private static GameObject hiddenMinimizedControls;

        private static void RestoreMinimizedControls()
        {
            // The game may have already reopened or discarded the backup.
            if (hiddenMinimizedControls && minimizedOwner && minimizedOwner.HasMinimizedOverlay())
                hiddenMinimizedControls.SetActive(true);
            hiddenMinimizedControls = null;
            minimizedOwner = null;
        }
        internal static bool IsPreviousFocus(GameObject target) => target == returnTo;
        internal static CalendarView View;

        [HarmonyPatch(typeof(OverlayManager), nameof(OverlayManager.OpenCalendar))]
        private static class Open
        {
            private static void Prefix()
            {
                returnTo = EventSystem.current?.currentSelectedGameObject;
                if (returnTo == null) returnTo = GamepadManager.Instance.GetGameObjectToSelect();
            }
            private static void Postfix(OverlayManager __instance, Transform __result, GameObject ___openMinimizedButton)
            {
                // The clock can open the calendar while a merchant is minimized. Its
                // reopen button and prompt share a separate root with their own input.
                if (__instance.IsCalendarOpen() && __instance.HasMinimizedOverlay() &&
                    ___openMinimizedButton && ___openMinimizedButton.activeSelf)
                {
                    minimizedOwner = __instance;
                    hiddenMinimizedControls = ___openMinimizedButton;
                    hiddenMinimizedControls.SetActive(false);
                }
                if (View != null && View.transform == __result) View.SelectToday();
            }
        }

        [HarmonyPatch(typeof(UICalendar), "OnEnable")]
        private static class Bind
        {
            private static void Postfix(UICalendar __instance, Transform ___calendarDays, GameObject ___lastDay,
                UnityEngine.Localization.LocalizedString ___stringBoss, UnityEngine.Localization.LocalizedString ___stringDayX)
            {
                View = __instance.GetComponent<CalendarView>() ?? __instance.gameObject.AddComponent<CalendarView>();
                View.Bind(___calendarDays, ___lastDay, ___stringBoss, ___stringDayX);
            }
        }

        [HarmonyPatch(typeof(UICalendar), nameof(UICalendar.OnEnterDay))]
        private static class Tooltip
        {
            private static bool Prefix(UICalendar __instance, int day)
            {
                var view = __instance.GetComponent<CalendarView>();
                return view == null || !view.Hover(day);
            }
        }

        // Vanilla translates the first keyboard binding to a legacy KeyCode. Read
        // the active input action instead so remaps work and opening is one press.
        [HarmonyPatch(typeof(UICalendar), "Update")]
        private static class CalendarKey
        {
            private static bool Prefix(UICalendar __instance)
            {
                if (View == null || View.gameObject != __instance.gameObject) return true;
                View.KeyboardToggle();
                return false;
            }
        }

        [HarmonyPatch(typeof(OverlayManager), nameof(OverlayManager.Cancel))]
        private static class Back
        {
            private static bool Prefix(OverlayManager __instance) =>
                !__instance.IsCalendarOpen() || View == null || !View.Back();
        }

        [HarmonyPatch(typeof(OverlayManager), nameof(OverlayManager.CloseOverlay))]
        private static class Close
        {
            private static void Prefix(OverlayManager __instance, out bool __state) => __state = __instance.IsCalendarOpen();
            private static void Postfix(OverlayManager __instance, bool __state)
            {
                if (!__state || __instance.IsCalendarOpen()) return;
                var picker = minimizedOwner == __instance && hiddenMinimizedControls && __instance.HasMinimizedOverlay()
                    ? hiddenMinimizedControls.transform.Find("Button")?.GetComponent<Button>() : null;
                if (minimizedOwner == __instance) RestoreMinimizedControls();
                var previous = returnTo ? returnTo.GetComponent<Selectable>() : null;
                if (returnTo && returnTo.activeInHierarchy && (!previous || previous.IsInteractable())) Focus(returnTo);
                else if (picker && picker.isActiveAndEnabled && picker.IsInteractable() && GamepadManager.Instance.IsGamepadInUse())
                    Focus(picker.gameObject);
                View = null;
                returnTo = null;
            }
        }

        [HarmonyPatch(typeof(UIClockButtonDay), "Start")]
        private static class Clock
        {
            private static void Postfix(UIClockButtonDay __instance) => DayBadge.Attach(__instance.transform);
        }

        [HarmonyPatch(typeof(UIResourceAmount), "Start")]
        private static class ResourceDay
        {
            private static void Postfix(UIResourceAmount __instance)
            {
                if (__instance is UIResourceAmountDay) DayBadge.Attach(__instance.transform);
            }
        }
    }
}
