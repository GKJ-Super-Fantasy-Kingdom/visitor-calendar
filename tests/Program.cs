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
Expect(16, Actors.Caravan);
schedule.Arrived.Add(Actors.Hermit);
Expect(16, Actors.Hermit, Actors.Caravan);
schedule.Arrived.Clear();
schedule.HermitInterval = 4;
schedule.CaravanStart = 8;
Expect(10, Actors.Hermit);
schedule.CaravansUnlocked = true;
Expect(10, Actors.Hermit, Actors.Caravan);
schedule.Challenge = true;
schedule.FixedDays[Actors.Hermit] = 5;
Expect(5, Actors.Hermit);
Expect(10);
schedule.LastDay = 15;
Expect(16);
Expect(0);
Console.WriteLine("Schedule checks passed (discovery, native, invitations, overrides, overlaps, no eligible recruit, challenge, day limits).");

void Expect(int day, params Actors[] expected)
{
    var actual = schedule.ForDay(day);
    if (!actual.OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x)))
        throw new Exception($"Day {day}: got {string.Join(", ", actual)}, expected {string.Join(", ", expected)}");
}

var dll = args.Length > 0 ? args[0] : Path.Combine("bin", "Release", "netstandard2.1", "VisitorCalendar.dll");
using var mod = AssemblyDefinition.ReadAssembly(dll);
using var game = AssemblyDefinition.ReadAssembly(typeof(Actors).Assembly.Location);
var count = 0;
foreach (var patch in Types(mod.MainModule.Types))
{
    var metadata = patch.CustomAttributes.SingleOrDefault(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch");
    if (metadata == null) continue;
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
if (args.Length == 3)
{
    const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
    var weekly = Assembly.LoadFrom(args[1]).GetType("WeeklyHermit.Schedule", true).GetMethod("IsRepeat", flags);
    var daily = Assembly.LoadFrom(args[2]).GetType("DailyCaravan.Visit", true).GetMethod("AddsVisit", flags);
    int scenarios = 0;
    foreach (bool challenge in new[] { false, true })
    foreach (bool known in new[] { false, true })
    foreach (bool priorUnlock in new[] { false, true })
    foreach (int interval in new[] { 1, 5, 7, 365 })
    foreach (int firstDay in new[] { 1, 10, 16, 999 })
    for (int day = 1; day <= 29; day++)
    {
        var model = new Schedule { Today = day, LastDay = 29, Challenge = challenge, HermitInterval = interval,
            CaravanStart = firstDay, CaravansUnlocked = priorUnlock || day > 14 };
        if (known) model.Known.UnionWith(new[] { Actors.Hermit, Actors.Caravan });
        var visits = model.ForDay(day);
        bool hermit = known && (bool)weekly.Invoke(null, new object[] { day, interval, challenge });
        bool caravan = (bool)daily.Invoke(null, new object[] { day, firstDay, known, model.CaravansUnlocked, challenge });
        if (visits.Contains(Actors.Hermit) != hermit || visits.Contains(Actors.Caravan) != caravan)
            throw new Exception($"Companion schedule mismatch at day {day}");
        scenarios++;
    }
    Console.WriteLine($"{scenarios} schedule comparisons passed against the supplied companion binaries.");
}

static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> roots)
{
    foreach (var type in roots)
    {
        yield return type;
        foreach (var nested in Types(type.NestedTypes)) yield return nested;
    }
}
