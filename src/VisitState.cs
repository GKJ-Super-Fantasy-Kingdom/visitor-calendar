using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.UI;
using UnityEngine;

namespace VisitorCalendar
{
    internal static class VisitState
    {
        internal static readonly HashSet<Actors> Arrived = new HashSet<Actors>();
        internal static readonly HashSet<Actors> Claimed = new HashSet<Actors>();
        internal static readonly VisitHistory History = new VisitHistory();
        internal static bool IsClaimed(Actors id)
        {
            if (id == Actors.Caravan)
            {
                var decision = Visitors.DailyCaravanDecision(DaytimeManager.Instance.CurrentDay());
                if (decision != CaravanDecision.Unavailable) return decision == CaravanDecision.Claimed;
            }
            return Claimed.Contains(id);
        }
        private static int day;
        private static GameDataManager scene;

        internal static void CheckDay(int currentDay)
        {
            if (scene != GameDataManager.Instance || day == currentDay) return;
            Remember();
            day = currentDay;
            Arrived.Clear();
            Claimed.Clear();
        }

        private static void Remember()
        {
            History.RecordCaravan(day, Visitors.DailyCaravanDecision(day));
            foreach (var actor in Arrived) History.Record(day, actor, false);
            foreach (var actor in Claimed) History.Record(day, actor, true);
        }

        // Tavern rewrites day and hash when advancing the same kingdom save.
        // lastPlayed is generated for each kingdom checkpoint and survives that rewrite.
        private static string HistoryPath(GameData data) =>
            data == null || string.IsNullOrEmpty(data.runID) || string.IsNullOrEmpty(data.lastPlayed) ? null :
            Path.Combine(Paths.ConfigPath, "VisitorCalendar", Helper.GenerateHash(string.Join("|",
                SettingsManager.Instance.GetProfile(), data.runID, data.lastPlayed)) + ".visits");

        // A native save may represent the following dawn. Carry claims only into an
        // identical same-day checkpoint. A retry from an earlier dawn starts clean.
        private static string PathFor(GameData data)
        {
            if (data == null || string.IsNullOrEmpty(data.runID) || string.IsNullOrEmpty(data.lastPlayed)) return null;
            return Path.Combine(Paths.ConfigPath, "VisitorCalendar",
                Helper.GenerateHash(string.Join("|", SettingsManager.Instance.GetProfile(), data.runID, data.day,
                    data.lastPlayed, data.hash)) + ".txt");
        }

        [HarmonyPatch(typeof(GameDataManager), nameof(GameDataManager.Load))]
        private static class Load
        {
            private static void Prefix(GameData ___m_GameData)
            {
                Claimed.Clear();
                Arrived.Clear();
                scene = null;
                History.Days.Clear();
                NativeBookings.BeginLoad();
                try
                {
                    var history = HistoryPath(___m_GameData);
                    if (history != null && File.Exists(history))
                    {
                        int today = ___m_GameData.day + 1;
                        History.Restore(File.ReadAllLines(history), today);
                        if (History.Days.TryGetValue(today, out var visits))
                            foreach (var visit in visits)
                            {
                                Arrived.Add(visit.Key);
                                if (visit.Value) Claimed.Add(visit.Key);
                            }
                        return;
                    }
                    var path = PathFor(___m_GameData);
                    if (path == null || !File.Exists(path)) return;
                    foreach (string line in File.ReadAllLines(path))
                        if (Enum.TryParse(line, out Actors actor) && actor != Actors.None) Claimed.Add(actor);
                }
                catch (IOException e) { Plugin.LogWarning(e); }
                catch (UnauthorizedAccessException e) { Plugin.LogWarning(e); }
            }
            private static void Postfix(GameDataManager __instance)
            {
                scene = __instance;
                day = DaytimeManager.Instance.CurrentDay();
            }
        }

        [HarmonyPatch(typeof(GameDataManager), "GenerateGameData")]
        private static class Snapshot
        {
            private static void Postfix(GameDataManager __instance, GameData __result)
            {
                if (__instance != scene || __result == null) return;
                Remember();
                if (History.Days.Count == 0) return;
                try
                {
                    string path = HistoryPath(__result);
                    if (path == null) return;
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    string temp = path + ".tmp";
                    File.WriteAllLines(temp, History.Lines());
                    if (File.Exists(path)) File.Replace(temp, path, null);
                    else File.Move(temp, path);
                }
                catch (IOException e) { Plugin.LogWarning(e); }
                catch (UnauthorizedAccessException e) { Plugin.LogWarning(e); }
            }
        }

        internal static void Arrival(Guest guest)
        {
            CheckDay(DaytimeManager.Instance.CurrentDay());
            var actor = Visitors.Identity(guest);
            if (actor != Actors.None) Arrived.Add(actor);
        }

        // Native merchant cards can show keywords, reroll, or cancel without granting
        // anything. Attribute a claim only when a card callback actually confirms.
        private static CardResolution resolving;
        private static readonly AccessTools.FieldRef<UIOverlayMerchant, Action<int, ResourceAmount[]>> Callback =
            AccessTools.FieldRefAccess<UIOverlayMerchant, Action<int, ResourceAmount[]>>("m_Callback");

        [HarmonyPatch(typeof(UIOverlayMerchant), nameof(UIOverlayMerchant.Init))]
        private static class ResetSpeaker
        {
            private static void Postfix(UIOverlayMerchant __instance)
            {
                var marker = __instance.GetComponent<MerchantSpeaker>();
                if (marker != null) marker.WeeklyOffer = false;
            }
        }

        [HarmonyPatch(typeof(UIOverlayMerchant), nameof(UIOverlayMerchant.SetCallback))]
        private static class WeeklyOffer
        {
            private static void Postfix(UIOverlayMerchant __instance, Action<int, ResourceAmount[]> cb)
            {
                var old = __instance.GetComponent<MerchantSpeaker>();
                if (old != null) old.WeeklyOffer = false;
                if (cb == null || !Chainloader.PluginInfos.TryGetValue(Visitors.Weekly, out var info) ||
                    info.Instance == null || cb.Method.DeclaringType?.Assembly != info.Instance.GetType().Assembly) return;
                var marker = __instance.GetComponent<MerchantSpeaker>() ?? __instance.gameObject.AddComponent<MerchantSpeaker>();
                marker.WeeklyOffer = true;
                CheckDay(DaytimeManager.Instance.CurrentDay());
                Arrived.Add(Actors.Hermit);
            }
        }

        [HarmonyPatch(typeof(UICard), nameof(UICard.Callback))]
        private static class Resolve
        {
            private static void Prefix(UICard __instance, out CardResolution __state)
            {
                __state = resolving;
                resolving = default;
                var screen = __instance.GetComponentInParent<UIOverlayMerchant>();
                if (screen == null) return;
                var callback = Callback(screen);
                if (callback?.Target is Guest guest) resolving.Actor = Visitors.Identity(guest);
                else if (screen.GetComponent<MerchantSpeaker>()?.WeeklyOffer == true && __instance is UICardUnit)
                    resolving.Actor = Actors.Hermit;
            }
            private static void Finalizer(CardResolution __state, Exception __exception)
            {
                resolving.Finish(Claimed, __exception == null);
                resolving = __state;
            }
        }

        [HarmonyPatch(typeof(OverlayManager), nameof(OverlayManager.Confirm))]
        private static class Confirm
        {
            private static void Postfix(bool __runOriginal)
            {
                if (__runOriginal) resolving.Confirmed = true;
            }
        }
    }

    internal sealed class MerchantSpeaker : MonoBehaviour
    {
        internal bool WeeklyOffer;
    }
}
