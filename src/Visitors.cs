using System;
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

        internal static Actors Identity(Guest guest)
        {
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

        private static int Setting(string guid, string key)
        {
            if (Chainloader.PluginInfos.TryGetValue(guid, out var info) && info.Instance != null &&
                info.Instance.Config.TryGetEntry<int>(new ConfigDefinition("Schedule", key), out var value))
                return value.Value;
            return 0;
        }

        internal static Schedule Read()
        {
            var race = RaceDataManager.Instance;
            var clock = DaytimeManager.Instance;
            VisitState.CheckDay(clock.CurrentDay());
            var challenge = MainManager.Instance.IsChallenge() ? ChallengeManager.Instance.GetLoadedChallenge() : null;
            var s = new Schedule {
                Today = clock.CurrentDay(), LastDay = clock.GetDayLimit(), Challenge = challenge != null,
                HermitInterval = Setting(Weekly, "IntervalDays"), CaravanStart = Setting(Daily, "FirstDay"),
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
            s.Arrived.UnionWith(VisitState.Arrived);
            s.Arrived.UnionWith(VisitState.Claimed);
            if (s.CaravanStart > 0 && CaravanClaimRestored()) s.Arrived.Add(Actors.Caravan);
            if (!s.Challenge && s.HermitInterval > 0 && s.Today > 2 && (s.Today - 2) % s.HermitInterval == 0 &&
                s.Known.Contains(Actors.Hermit) && !s.Arrived.Contains(Actors.Hermit))
                s.HermitOfferPossible = CanOfferRecruit();
            return s;

            void Add(Actors actor, int day, RaceEvent discovery)
            {
                s.FixedDays.Add(actor, day);
                bool known = race.HasRaceEventFlag(discovery);
                if (actor == Actors.Hermit)
                    known |= race.GetDialogData(actor).Count != 0 || DialogueManager.Instance.HasSpokenToThisRun(actor);
                if (known) s.Known.Add(actor);
            }
        }

        private static bool CanOfferRecruit()
        {
            var unlocks = UnlockedUnitsManager.Instance;
            var race = RaceManager.Instance.GetRace();
            var banned = unlocks.GetBannedUnits();
            foreach (string id in unlocks.GetUnlocked())
            {
                if (banned.Contains(id) || !UnitManager.Instance.CanAddUnitTypeExclusive(id)) continue;
                var unit = AddressablesManager.Instance.GetUnit(id);
                if (unit != null && unit.GetRace() != race && unit.GetRace() != Races.Elemental) return true;
            }
            return false;
        }

        internal static bool CaravanClaimRestored()
        {
            // Daily Caravan 0.1 owns replayed rewards. Read only the current scene's record.
            if (!Chainloader.PluginInfos.TryGetValue(Daily, out var info) || info.Instance == null) return false;
            var plugin = info.Instance;
            var type = plugin.GetType();
            if (!ReferenceEquals(AccessTools.Field(type, "scene")?.GetValue(plugin), GameDataManager.Instance) ||
                !Equals(AccessTools.Field(type, "day")?.GetValue(plugin), DaytimeManager.Instance.CurrentDay())) return false;
            var visit = AccessTools.Field(type, "Visit")?.GetValue(plugin);
            return visit != null && !string.IsNullOrEmpty(AccessTools.Field(visit.GetType(), "selected")?.GetValue(visit) as string);
        }

        internal static Actor Actor(Actors id) => DialogueManager.Instance.GetActor(id);
        internal static string Title(Actors id)
        {
            var actor = Actor(id);
            return actor != null ? actor.stringTitle.GetLocalizedString() : id.ToString();
        }
    }
}
