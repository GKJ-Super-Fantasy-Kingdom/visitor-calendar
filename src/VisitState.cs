using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
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
        internal static bool IsClaimed(Actors id) => Claimed.Contains(id) ||
            (id == Actors.Caravan && Visitors.CaravanClaimRestored());
        private static int day;
        private static GameDataManager scene;

        internal static void CheckDay(int currentDay)
        {
            if (scene != GameDataManager.Instance || day == currentDay) return;
            day = currentDay;
            Arrived.Clear();
            Claimed.Clear();
        }

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
                try
                {
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
                if (__instance != scene || __result == null || __result.day + 1 != day || Claimed.Count == 0) return;
                try
                {
                    string path = PathFor(__result);
                    if (path == null) return;
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    string temp = path + ".tmp";
                    File.WriteAllLines(temp, Claimed.Select(actor => actor.ToString()));
                    if (File.Exists(path)) File.Replace(temp, path, null);
                    else File.Move(temp, path);
                }
                catch (IOException e) { Plugin.LogWarning(e); }
                catch (UnauthorizedAccessException e) { Plugin.LogWarning(e); }
            }
        }

        [HarmonyPatch(typeof(Guest), "OnLoad")]
        private static class Arrival
        {
            private static void Postfix(Guest __instance)
            {
                var actor = Visitors.Identity(__instance);
                if (actor != Actors.None) Arrived.Add(actor);
            }
        }

        // Native merchant cards can show keywords, reroll, or cancel without granting
        // anything. Attribute a claim only when a card callback actually confirms.
        private static Actors resolving;
        private static bool confirmed;
        private static readonly AccessTools.FieldRef<UIOverlayMerchant, Action<int, ResourceAmount[]>> Callback =
            AccessTools.FieldRefAccess<UIOverlayMerchant, Action<int, ResourceAmount[]>>("m_Callback");

        [HarmonyPatch(typeof(UIOverlayMerchant), nameof(UIOverlayMerchant.Init))]
        private static class ResetSpeaker
        {
            private static void Postfix(UIOverlayMerchant __instance)
            {
                var marker = __instance.GetComponent<MerchantSpeaker>();
                if (marker != null) marker.IsHermit = false;
            }
        }

        [HarmonyPatch(typeof(UIOverlayMerchant), nameof(UIOverlayMerchant.SetActor))]
        private static class Speaker
        {
            private static void Postfix(UIOverlayMerchant __instance, Actor __0)
            {
                if (__0 == null || __0 != Visitors.Actor(Actors.Hermit)) return;
                var marker = __instance.GetComponent<MerchantSpeaker>() ?? __instance.gameObject.AddComponent<MerchantSpeaker>();
                marker.IsHermit = true;
            }
        }

        [HarmonyPatch(typeof(UICard), nameof(UICard.Callback))]
        private static class Resolve
        {
            private static void Prefix(UICard __instance, out (Actors Actor, bool Confirmed) __state)
            {
                __state = (resolving, confirmed);
                resolving = Actors.None;
                confirmed = false;
                var screen = __instance.GetComponentInParent<UIOverlayMerchant>();
                if (screen == null) return;
                var callback = Callback(screen);
                if (callback?.Target is Guest guest) resolving = Visitors.Identity(guest);
                else if (screen.GetComponent<MerchantSpeaker>()?.IsHermit == true && __instance is UICardUnit)
                    resolving = Actors.Hermit;
            }
            private static void Finalizer((Actors Actor, bool Confirmed) __state, Exception __exception)
            {
                if (__exception == null && confirmed && resolving != Actors.None) Claimed.Add(resolving);
                resolving = __state.Actor;
                confirmed = __state.Confirmed;
            }
        }

        [HarmonyPatch(typeof(OverlayManager), nameof(OverlayManager.Confirm))]
        private static class Confirm
        {
            private static void Postfix()
            {
                if (resolving != Actors.None) confirmed = true;
            }
        }
    }

    internal sealed class MerchantSpeaker : MonoBehaviour
    {
        internal bool IsHermit;
    }
}
