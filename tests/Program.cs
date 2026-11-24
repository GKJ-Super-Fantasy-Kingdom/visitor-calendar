using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using SuperFantasyKingdom;
using VisitorCalendar;

var schedule = new Schedule { Today = 16, LastDay = 29 };
schedule.FixedDays[Actors.Hermit] = 2;
schedule.FixedDays[Actors.Caravan] = 16;
schedule.Invitations[1] = (int)Actors.Caravan;
schedule.Invitations[2] = (int)Actors.WizardUnknown;
schedule.Arrived.Add(Actors.Caravan);
Expect(16);
schedule.Known.UnionWith(new[] { Actors.Caravan, Actors.Hermit, Actors.Wizard });
schedule.Today = 1;
Expect(2, Actors.Hermit);
Expect(9);
Expect(14, Actors.Caravan);
Expect(16, Actors.Caravan);
Expect(21, Actors.Wizard);
schedule.HermitInterval = 7;
Expect(9, Actors.Hermit);
Expect(16, Actors.Hermit, Actors.Caravan);
schedule.CaravanStart = 16;
Expect(17, Actors.Caravan);
Expect(23, Actors.Caravan, Actors.Hermit);
schedule.HermitOfferPossible = false;
schedule.Today = 16;
Expect(16, Actors.Caravan);
schedule.Arrived.Add(Actors.Hermit);
Expect(16, Actors.Hermit, Actors.Caravan);
schedule.Arrived.Clear();
schedule.HermitInterval = 4;
schedule.CaravanStart = 8;
schedule.Today = 3;
Expect(10, Actors.Hermit);
schedule.CaravansUnlocked = true;
Expect(10, Actors.Hermit, Actors.Caravan);
schedule.VanillaCaravanUnlock = false;
schedule.Today = 16;
Expect(17);
Expect(16, Actors.Caravan);
schedule.VanillaCaravanUnlock = true;
schedule.Challenge = true;
schedule.Today = 1;
schedule.FixedDays[Actors.Hermit] = 5;
Expect(5, Actors.Hermit);
Expect(10);
schedule.LastDay = 15;
Expect(16);
Expect(0);
Console.WriteLine("Schedule checks passed (discovery, native, invitations, overrides, overlaps, no eligible recruit, challenge, day limits).");

schedule = new Schedule { Today = 15, LastDay = 29 };
var added = (Actors)7001;
schedule.Arrived.Add(added);
Expect(15);
schedule.Known.UnionWith(new[] { added, Actors.Hermit, Actors.Caravan });
schedule.FixedDays[Actors.Hermit] = 11;
Expect(2);
schedule.Today = 3;
Expect(11, Actors.Hermit);
schedule.ArrivalOnly.Add(Actors.Hermit);
Expect(11);
schedule.Today = 15;
schedule.Arrived.Add(Actors.Hermit);
Expect(15, added, Actors.Hermit);
Expect(16);
schedule.Invitations[2] = (int)added;
Expect(21, added);
schedule.SharedArrivalOverride = true;
Expect(21);
Expect(15, added, Actors.Hermit);
schedule.History.Record(12, added, false);
Expect(12, added);
schedule.SharedArrivalOverride = false;
Expect(21, added);
if (Schedule.CellMarker(false, 0, 2) != "•" || Schedule.CellMarker(true, 1, 2) != null ||
    Schedule.CellMarker(false, 2, 7) != "+5" || Schedule.CellMarker(true, 1, 3, 2) != "+2" ||
    Schedule.CellMarker(true, 1, 2, 2) != null) throw new Exception("Missing portrait or overlap marker");
var claims = new HashSet<Actors>();
new CardResolution { Actor = Actors.Hermit }.Finish(claims, true);
new CardResolution { Confirmed = true }.Finish(claims, true);
new CardResolution { Actor = added, Confirmed = true }.Finish(claims, false);
if (claims.Count != 0) throw new Exception("Preview, unrelated confirm or failed purchase created a claim");
new CardResolution { Actor = added, Confirmed = true }.Finish(claims, true);
if (!claims.SetEquals(new[] { added })) throw new Exception("Shared day contaminated another claim");
Console.WriteLine("Custom arrival, missing portrait, moved/opaque dates and per-visitor claim fixtures passed.");

schedule = new Schedule { Today = 6, LastDay = 29, HermitInterval = 7, CaravanStart = 16 };
schedule.Known.UnionWith(new[] { Actors.Hermit, Actors.Merchant, Actors.Caravan });
schedule.FixedDays[Actors.Merchant] = 5;
Expect(5); // A past forecast cannot create a visit.
schedule.History.Record(2, Actors.Hermit, true);
schedule.History.Record(2, Actors.Hermit, false);
schedule.History.Record(5, Actors.Merchant, false);
var restored = new VisitHistory();
restored.Restore(schedule.History.Lines().Concat(new[] { "broken", "25|80|1" }), 6);
schedule.History = restored;
Expect(2, Actors.Hermit);
Expect(5, Actors.Merchant);
Expect(9, Actors.Hermit);
Expect(16, Actors.Hermit, Actors.Caravan);
if (schedule.Status(2, Actors.Hermit, false) != "Visited · Claimed" ||
    schedule.Status(5, Actors.Merchant, false) != "Visited" ||
    schedule.Status(16, Actors.Caravan, false) != "Forecast" || restored.Days.ContainsKey(25))
    throw new Exception("Visit history confused a forecast, arrival, claim or later checkpoint");
schedule.CaravanGateOpened = false;
schedule.FixedDays[Actors.Caravan] = 16;
Expect(16, Actors.Hermit);
schedule.History.Record(5, Actors.Caravan, false);
Expect(5, Actors.Merchant, Actors.Caravan);
schedule.AllowBeforeGiantGate = true;
schedule.CaravanStart = 17;
Expect(16, Actors.Hermit); // The separate native booking still requires the gate.
schedule.Known.Remove(Actors.Caravan);
Expect(17, Actors.Caravan); // An explicitly enabled extra wagon can be forecast before discovery.
schedule.CaravanStart = 7;
Expect(7);
schedule.CaravansUnlocked = true;
Expect(7, Actors.Caravan);
schedule.Challenge = true;
Expect(7);
schedule.Challenge = false;
schedule.CaravanStart = 0;
Expect(17);
Console.WriteLine("Observed history, distinct claims, rollback and forecast checks passed.");

var orders = new Schedule { Today = 16, LastDay = 28 };
orders.Known.Add(Actors.Caravan);
orders.History.Record(16, Actors.Caravan, true);
orders.FixedDays[Actors.Caravan] = 20;
orders.Invitations[2] = (int)Actors.Caravan;
int confirmedDay = -1;
bool orderPending = true;
bool Commission(out int day) { day = confirmedDay; return orderPending; }
bool Due(out string title, out int quantity, out string resource, out int deadline)
{
    title = "Bread for the Watch"; quantity = 6; resource = "Bread"; deadline = 20;
    return true;
}
var sharedDate = CalendarNotice.Read(orders, Due, Commission, e => throw e);
if (sharedDate.Count != 2 || sharedDate.Any(n => n.Day != 20) || sharedDate[0].IsCommission ||
    !sharedDate[1].IsCommission || !orders.ForDay(20).Contains(Actors.Caravan))
    throw new Exception("A shared date must retain separate deadline, commission and visitor cues");
void CheckCommission(string title, int? day)
{
    var entry = CalendarNotice.Read(orders, null, Commission, e => throw e).Single();
    if (entry.Title != title || entry.Day != day) throw new Exception($"Wrong commission: {entry.Title} on {entry.Day}");
}
CheckCommission("Commission Forecast", 20);
orders.ConsumedCaravanDay = 20;
CheckCommission("Commission Forecast", 21);
orders.SharedArrivalOverride = true;
CheckCommission("Next Caravan", null);
confirmedDay = 16;
CheckCommission("Commission Delivery", 16); // A distinct, eligible visit confirmed by the provider.
confirmedDay = -1;
orders.SharedArrivalOverride = false;
orders.CaravanGateOpened = false;
CheckCommission("Next Caravan", null);
orders.CaravanGateOpened = true;
orders.LastDay = 18;
orders.CaravanStart = 17;
CheckCommission("Commission Forecast", 17);
orders.Challenge = true;
CheckCommission("Next Caravan", null);
orderPending = false;
if (CalendarNotice.Read(orders, null, Commission, e => throw e).Count != 0 ||
    !orders.History.Days[16][Actors.Caravan]) throw new Exception("Clearing an order changed visit history");
Console.WriteLine("Commission checks passed (distinct visit today, bookings, opaque dates, gate, daily and challenge).");

void Expect(int day, params Actors[] expected)
{
    var actual = schedule.ForDay(day);
    if (!actual.OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x)))
        throw new Exception($"Day {day}: got {string.Join(", ", actual)}, expected {string.Join(", ", expected)}");
}

if (!CalendarShortcut.CanAdvertise(true, true, false, false) ||
    CalendarShortcut.CanAdvertise(false, true, false, false) || CalendarShortcut.CanAdvertise(true, false, false, false) ||
    CalendarShortcut.CanAdvertise(true, true, true, false) || CalendarShortcut.CanAdvertise(true, true, false, true))
    throw new Exception("Calendar shortcut disagrees with the native day button");
var readme = File.ReadAllText("README.md");
if (!readme.Contains(CalendarShortcut.OpenAction) || !readme.Contains(CalendarShortcut.KeyboardBackAction) ||
    !readme.Contains("https://github.com/GKJ-Super-Fantasy-Kingdom/visitor-calendar/blob/main/CONTRIBUTING.md"))
    throw new Exception("Player guide lost an input action or its standalone package contribution link");
Console.WriteLine("Native shortcut conditions, independent map/overlay actions and packaged-guide link passed.");

var dll = args.Length > 0 ? args[0] : Path.Combine("bin", "Release", "netstandard2.1", "VisitorCalendar.dll");
if (!File.ReadAllBytes(typeof(Schedule).Assembly.Location).SequenceEqual(File.ReadAllBytes(dll)))
    throw new Exception("The scheduling tests must run against the DLL supplied for verification");
using var mod = AssemblyDefinition.ReadAssembly(dll);
using var game = AssemblyDefinition.ReadAssembly(typeof(Actors).Assembly.Location);
var count = 0;
foreach (var patch in Types(mod.MainModule.Types))
{
    var metadata = patch.CustomAttributes.SingleOrDefault(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch");
    if (metadata == null) continue;
    if (metadata.ConstructorArguments.Count == 0 && patch.Name == "NativeBookings")
    {
        var targets = patch.Methods.Single(m => m.Name == ".cctor").Body.Instructions
            .Where(i => i.OpCode == Mono.Cecil.Cil.OpCodes.Ldtoken).Select(i => (TypeReference)i.Operand).ToList();
        if (targets.Count != 10) throw new Exception("Expected ten observed native guest schedulers");
        foreach (var targetType in targets)
        {
            var getDay = game.MainModule.GetType(targetType.FullName).Methods.Single(m => m.Name == "GetDay");
            if (getDay.ReturnType.FullName != "System.Int32" || getDay.HasParameters)
                throw new Exception("Guest.GetDay signature changed");
            count++;
        }
        continue;
    }
    var target = game.MainModule.GetType(((TypeReference)metadata.ConstructorArguments[0].Value).FullName);
    var name = (string)metadata.ConstructorArguments[1].Value;
    var method = target.Methods.Single(m => m.Name == name);
    foreach (var hook in patch.Methods.Where(m => m.Name == "Prefix" || m.Name == "Postfix" || m.Name == "Finalizer"))
    {
        foreach (var arg in hook.Parameters)
        {
            string actual = arg.ParameterType.FullName.TrimEnd('&');
            string required;
            if (arg.Name.StartsWith("___"))
                required = target.Fields.Single(f => f.Name == arg.Name.Substring(3)).FieldType.FullName;
            else if (arg.Name == "__instance") required = target.FullName;
            else if (arg.Name == "__result") required = method.ReturnType.FullName;
            else if (arg.Name == "__state") continue;
            else if (arg.Name == "__exception") required = typeof(Exception).FullName;
            else if (arg.Name == "__runOriginal") required = typeof(bool).FullName;
            else if (arg.Name.StartsWith("__")) required = method.Parameters[int.Parse(arg.Name.Substring(2))].ParameterType.FullName;
            else required = method.Parameters.Single(p => p.Name == arg.Name).ParameterType.FullName;
            if (actual != required) throw new Exception($"Invalid {target.Name}.{name} hook parameter {arg.Name}");
        }
    }
    ++count;
}
if (count == 0) throw new Exception("No patch targets found");
Console.WriteLine($"{count} Harmony patch targets and parameter signatures matched the installed game assembly.");

// Optional comparison against local companion builds. Neither is a game dependency.
if (args.Length >= 3)
{
    const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
    var weekly = Assembly.LoadFrom(args[1]).GetType("WeeklyHermit.Schedule", true).GetMethod("IsRepeat", flags);
    // A restored provider decision must work even if no HUD was visible before saving.
    var companion = Assembly.LoadFrom(args[2]);
    var record = Activator.CreateInstance(companion.GetType("DailyCaravan.Visit", true));
    var kingdom = record.GetType().GetField("kingdom");
    var declined = record.GetType().GetField("declined");
    var selected = record.GetType().GetField("selected");
    var history = new VisitHistory();
    if (CaravanReplay.Read(record) != CaravanDecision.Unavailable)
        throw new Exception("Unassigned caravan created an appointment");
    kingdom.SetValue(record, 1);
    if (CaravanReplay.Read(record) != CaravanDecision.Pending)
        throw new Exception("A queued caravan was counted as visited");
    declined.SetValue(record, true);
    var outcome = CaravanReplay.Read(record);
    if (outcome != CaravanDecision.Declined) throw new Exception("Declined caravan lost its replay decision");
    history.RecordCaravan(16, outcome);
    schedule = new Schedule { Today = 16, LastDay = 29, CaravanStart = 16, History = history };
    schedule.Known.Add(Actors.Caravan);
    Expect(16, Actors.Caravan);
    if (schedule.Status(16, Actors.Caravan, false) != "Visited") throw new Exception("Restored decline shown as forecast");
    schedule.Today = 17;
    schedule.History = new VisitHistory();
    schedule.History.Restore(history.Lines(), 17);
    Expect(16, Actors.Caravan);
    if (schedule.Status(16, Actors.Caravan, false) != "Visited") throw new Exception("Decline lost after next save");
    selected.SetValue(record, "test-item");
    declined.SetValue(record, false);
    schedule.History.RecordCaravan(16, CaravanReplay.Read(record));
    if (schedule.Status(16, Actors.Caravan, false) != "Visited · Claimed")
        throw new Exception("Selected caravan reward lost its claim");
    var pending = new VisitHistory();
    pending.RecordCaravan(16, CaravanDecision.Pending);
    pending.RecordCaravan(16, CaravanDecision.Unavailable);
    if (pending.Days.Count != 0) throw new Exception("Unconfirmed caravan became history");
    Console.WriteLine("Restored Daily Caravan decline, claim, pending and next-checkpoint cases passed.");
    var daily = Assembly.LoadFrom(args[2]).GetType("DailyCaravan.Visit", true).GetMethod("AddsVisit", flags);
    int scenarios = 0;
    foreach (bool challenge in new[] { false, true })
    foreach (bool known in new[] { false, true })
    foreach (bool allowBeforeGate in new[] { false, true })
    foreach (bool priorUnlock in new[] { false, true })
    foreach (int interval in new[] { 1, 5, 7, 365 })
    foreach (int firstDay in new[] { 1, 10, 16, 999 })
    for (int day = 1; day <= 29; day++)
    {
        var model = new Schedule { Today = day, LastDay = 29, Challenge = challenge, HermitInterval = interval,
            CaravanStart = firstDay, CaravanGateOpened = known, AllowBeforeGiantGate = allowBeforeGate,
            CaravansUnlocked = priorUnlock || day > 14 };
        if (known) model.Known.UnionWith(new[] { Actors.Hermit, Actors.Caravan });
        var visits = model.ForDay(day);
        bool hermit = known && (bool)weekly.Invoke(null, new object[] { day, interval, challenge });
        bool caravan = (bool)daily.Invoke(null, new object[] { day, firstDay, known || allowBeforeGate, model.CaravansUnlocked, challenge });
        if (visits.Contains(Actors.Hermit) != hermit || visits.Contains(Actors.Caravan) != caravan)
            throw new Exception($"Companion schedule mismatch at day {day}");
        scenarios++;
    }
    Console.WriteLine($"{scenarios} schedule comparisons passed against the supplied companion binaries.");
}

if (args.Length >= 4)
{
    var asm = Assembly.LoadFrom(args[3]);
    var charter = asm.GetType("KingdomCharters.Charter", true);
    var state = asm.GetType("KingdomCharters.CharterState", true);
    var merchant = state.GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Static)
        .Invoke(null, new[] { Enum.Parse(charter, "Merchant") });
    var visitToday = state.GetMethod("VisitToday", BindingFlags.NonPublic | BindingFlags.Instance);
    var record = new CharterFixture { Run = merchant, RunId = "test-run" };
    if (MerchantBooking.FirstVisit(record, "other-run", false, false) != null ||
        MerchantBooking.FirstVisit(record, "test-run", false, false) != 0 ||
        MerchantBooking.FirstVisit(record, "test-run", true, false) != null)
        throw new Exception("Charter applicability mismatch");
    var allThree = MerchantBooking.FirstVisit(record, "test-run", false, true);
    if (allThree.HasValue) throw new Exception("Daily Caravan should disable the Merchant charter");
    schedule = new Schedule { Today = 16, LastDay = 29, HermitInterval = 7, CaravanStart = 16,
        PendingMerchantCharter = allThree == 0, ConsumedCaravanDay = allThree ?? 0 };
    schedule.Known.UnionWith(new[] { Actors.Hermit, Actors.Caravan });
    Expect(16, Actors.Hermit, Actors.Caravan);
    foreach (int booked in new[] { 7, 16 })
    {
        state.GetField("firstVisit").SetValue(merchant, 0);
        schedule = new Schedule { Today = booked - 2, LastDay = 29, PendingMerchantCharter = true };
        schedule.Known.Add(Actors.Caravan);
        schedule.FixedDays[Actors.Caravan] = 16;
        if (booked == 7) schedule.Invitations[0] = (int)Actors.Caravan;
        Expect(booked - 1);
        Expect(booked);
        schedule.Today++;
        Expect(booked, Actors.Caravan); // No early arrival on a busy day.
        if (!(bool)visitToday.Invoke(merchant, new object[] { booked - 1, false, true }))
            throw new Exception("Installed charter didn't accept the free early slot");
        state.GetField("firstVisit").SetValue(merchant, booked);
        schedule.PendingMerchantCharter = false;
        schedule.ConsumedCaravanDay = MerchantBooking.FirstVisit(record, "test-run", false, false).Value;
        schedule.Arrived.Add(Actors.Caravan);
        Expect(booked - 1, Actors.Caravan);
        if ((bool)visitToday.Invoke(merchant, new object[] { booked, true, false }))
            throw new Exception("Installed charter repeated the original booking");
        Expect(booked);
    }
    Console.WriteLine("Kingdom Charters binary policy and calendar fixtures passed for early, occupied, consumed and stale-run bookings.");
}

static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> roots)
{
    foreach (var type in roots)
    {
        yield return type;
        foreach (var nested in Types(type.NestedTypes)) yield return nested;
    }
}

internal sealed class CharterFixture
{
    public string RunId;
    public object Run;
}
