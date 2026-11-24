using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using SuperFantasyKingdom;

namespace VisitorCalendar
{
    internal static class Visitors
    {
        internal const string Weekly = "gkj.superfantasykingdom.weeklyhermit";
        internal const string Daily = "sfk.dailycaravan";
        internal const string Charters = "gkj.sfk.kingdomcharters";
        private static readonly FieldInfo DialogueId = typeof(DialogueCollection).GetField("actor",
            BindingFlags.Instance | BindingFlags.NonPublic);

        internal static Actors Identity(Guest guest)
        {
            if (guest == null) return Actors.None;
            if (guest.GetType().Assembly != typeof(Guest).Assembly)
            {
                var dialogue = guest.GetDialogueCollection();
                return dialogue != null && DialogueId?.GetValue(dialogue) is Actors id ? id : Actors.None;
            }
            switch (guest)
            {
                case GuestHermit _: return Actors.Hermit;
                case GuestMerchant _: return Actors.Merchant;
                case GuestMonsterHunter _: return Actors.MonsterHunter;
                case GuestWitchDoctor _: return Actors.WitchDoctor;
                case GuestGnomes _: return Actors.Gnomes;
                case GuestBuilder _: case GuestSiege _: return Actors.Builder;
                case GuestDemolisher _: return Actors.Demolisher;
                case GuestCaravan _: return Actors.Caravan;
                case GuestWizard _: return Actors.Wizard;
                case GuestResearcher _: return Actors.Researcher;
                case GuestBlacksmith _: return Actors.Blacksmith;
                default: return Actors.None;
            }
        }

        private static T Setting<T>(string guid, string key, T fallback)
        {
            if (Chainloader.PluginInfos.TryGetValue(guid, out var info) && info.Instance != null &&
                info.Instance.Config.TryGetEntry<T>(new ConfigDefinition("Schedule", key), out var value))
                return value.Value;
            return fallback;
        }

        internal static Schedule Read()
        {
            var race = RaceDataManager.Instance;
            var clock = DaytimeManager.Instance;
            VisitState.CheckDay(clock.CurrentDay());
            var challenge = MainManager.Instance.IsChallenge() ? ChallengeManager.Instance.GetLoadedChallenge() : null;
            var s = new Schedule {
                Today = clock.CurrentDay(), LastDay = clock.GetDayLimit(), Challenge = challenge != null,
                HermitInterval = Setting(Weekly, "IntervalDays", 0), CaravanStart = Setting(Daily, "FirstDay", 0),
                AllowBeforeGiantGate = Setting(Daily, "AllowBeforeGiantGate", false),
                History = VisitState.History, CaravanGateOpened = race.HasRaceEventFlag(RaceEvent.GateOpened),
                CaravansUnlocked = WorldManager.Instance.HasUnlockedCaravans()
            };
            Add(Actors.Hermit, challenge?.guestHermit ?? 2, RaceEvent.HermitInQuestion);
            Add(Actors.Merchant, challenge?.guestMerchant ?? 5, RaceEvent.MerchantHelped);
            Add(Actors.MonsterHunter, challenge?.guestMonsterHunter ?? 6, RaceEvent.MonsterHunterMet);
            Add(Actors.WitchDoctor, challenge?.guestWitchDoctor ?? 9, RaceEvent.WitchDoctorMet);
            Add(Actors.Gnomes, challenge?.guestGnomes ?? 12, RaceEvent.GnomesHelped);
            Add(Actors.Builder, challenge?.guestBuilder ?? 13, RaceEvent.BuilderUnlocked);
            Add(Actors.Caravan, challenge?.guestCaravan ?? 16, RaceEvent.GateOpened);
            Add(Actors.Demolisher, challenge?.guestDemolisher ?? 19, RaceEvent.DemolisherUnlocked);
            Add(Actors.Wizard, challenge?.guestWizard ?? 23, RaceEvent.WizardUnlocked);
            Add(Actors.Researcher, challenge?.guestResearcher ?? 26, RaceEvent.ResearcherUnlocked);
            if (race.HasRaceEventFlag(RaceEvent.BlackSmithMet)) s.Known.Add(Actors.Blacksmith);
            s.Invitations[0] = race.GetGuestWeek1();
            s.Invitations[1] = race.GetGuestWeek2();
            s.Invitations[2] = race.GetGuestWeek3();
            s.Invitations[3] = race.GetGuestWeek4();
            s.SharedArrivalOverride = NativeBookings.SharedArrivalOverride;
            s.ArrivalOnly.UnionWith(NativeBookings.ArrivalOnly);
            s.VanillaCaravanUnlock = !NativeBookings.CaravanUnlockPatched;
            if (Chainloader.PluginInfos.TryGetValue(Charters, out var charter) && charter.Instance != null)
            {
                int? originalDay = MerchantBooking.FirstVisit(charter.Instance, GameDataManager.Instance.GetRunID(),
                    s.Challenge, Chainloader.PluginInfos.ContainsKey(Daily));
                s.ConsumedCaravanDay = originalDay ?? 0;
                s.PendingMerchantCharter = originalDay == 0;
            }
            var decision = DailyCaravanDecision(s.Today);
            if (decision == CaravanDecision.Claimed || decision == CaravanDecision.Declined)
                VisitState.Arrived.Add(Actors.Caravan);
            s.Arrived.UnionWith(VisitState.Arrived);
            s.Arrived.UnionWith(VisitState.Claimed);
            foreach (var visits in s.History.Days.Values)
                foreach (var actor in visits.Keys) DiscoverArrival(actor);
            foreach (var actor in s.Arrived) DiscoverArrival(actor);
            // A newly registered actor can be identified by a real invitation or
            // arrival and a recorded encounter. Never map an unfamiliar ID to a date.
            foreach (int id in s.Invitations) Discover((Actors)id);
            // The repeat offer may wait for another guest or be vetoed by a
            // patched recruitment pool. Its merchant callback confirms eligibility.
            s.HermitOfferPossible = s.Arrived.Contains(Actors.Hermit);
            return s;

            void DiscoverArrival(Actors actor)
            {
                if (s.FixedDays.ContainsKey(actor) || actor == Actors.Blacksmith) s.Known.Add(actor);
                else Discover(actor);
            }

            void Add(Actors actor, int day, RaceEvent discovery)
            {
                s.FixedDays.Add(actor, NativeBookings.FixedDay(actor, day));
                bool known = race.HasRaceEventFlag(discovery);
                if (actor == Actors.Hermit)
                    known |= race.GetDialogData(actor).Count != 0 || DialogueManager.Instance.HasSpokenToThisRun(actor);
                if (known) s.Known.Add(actor);
            }

            void Discover(Actors actor)
            {
                if (actor == Actors.None || s.Known.Contains(actor) || s.FixedDays.ContainsKey(actor) ||
                    actor == Actors.WizardUnknown || Actor(actor) == null) return;
                if (DialogueManager.Instance.HasSpokenToThisRun(actor) || race.GetDialogData(actor).Count > 0)
                    s.Known.Add(actor);
            }
        }

        internal static CaravanDecision DailyCaravanDecision(int day)
        {
            if (!Chainloader.PluginInfos.TryGetValue(Daily, out var info) || info.Instance == null)
                return CaravanDecision.Unavailable;
            var plugin = info.Instance;
            var type = plugin.GetType();
            if (!ReferenceEquals(AccessTools.Field(type, "scene")?.GetValue(plugin), GameDataManager.Instance) ||
                !Equals(AccessTools.Field(type, "day")?.GetValue(plugin), day))
                return CaravanDecision.Unavailable;
            return CaravanReplay.Read(AccessTools.Field(type, "Visit")?.GetValue(plugin));
        }

        internal static Actor Actor(Actors id) => DialogueManager.Instance.GetActor(id);
        internal static UnityEngine.Sprite Portrait(Actors id)
        {
            var actor = Actor(id);
            return actor != null ? actor.avatar : null;
        }
        internal static string Title(Actors id)
        {
            var actor = Actor(id);
            if (actor != null)
            {
                foreach (var text in new[] { actor.stringTitle, actor.stringName })
                    if (text != null && !text.IsEmpty)
                    {
                        string value = text.GetLocalizedString();
                        if (!string.IsNullOrWhiteSpace(value)) return value;
                    }
                if (!string.IsNullOrWhiteSpace(actor.title)) return actor.title;
            }
            return Enum.IsDefined(typeof(Actors), id) ? id.ToString() : "Visitor";
        }
    }
}
