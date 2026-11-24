# Run in a fresh Windows PowerShell process with DLLs extracted from local test ZIPs.
param(
    [Parameter(Mandatory)] [string] $PluginDir,
    [Parameter(Mandatory)] [string] $CopiedSave,
    [Parameter(Mandatory)] [string] $ManagedDir,
    [Parameter(Mandatory)] [string] $CoreDir
)
$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.IO;
using System.Reflection;
public static class CalendarTestLoader {
    public static void Use(params string[] dirs) {
        AppDomain.CurrentDomain.AssemblyResolve += (s,e) => {
            foreach (string dir in dirs) {
                var path = Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll");
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
    }
}
'@
[CalendarTestLoader]::Use($ManagedDir, $CoreDir)
$all = [Reflection.BindingFlags]'Instance,Static,Public,NonPublic'
function Set-Field($obj, $name, $value) {
    $obj.GetType().GetField($name, $all).SetValue($obj, $value)
}
function New-SceneObject($type) {
    $obj = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($type)
    [UnityEngine.Object].GetField('m_CachedPtr', $all).SetValue($obj, [IntPtr]1)
    $type.GetField('Instance', $all).SetValue($null, $obj)
    return $obj
}
function Package($name) {
    $path = Join-Path $PluginDir "$name.dll"
    if (Test-Path -LiteralPath $path) { return [Reflection.Assembly]::LoadFrom($path) }
}
$game = [Reflection.Assembly]::LoadFrom((Join-Path $ManagedDir 'Assembly-CSharp.dll'))
$calendar = Package VisitorCalendar
$weekly = Package WeeklyContracts
$caravan = Package CaravanCommissions
$bridge = $calendar.GetType('VisitorCalendar.PartnerCalendar', $true)
$weeklyArgs = [object[]]::new(1)
$caravanArgs = [object[]]::new(1)
if ($weekly) { $weeklyArgs[0] = $weekly }
if ($caravan) { $caravanArgs[0] = $caravan }
$contractQuery = $bridge.GetMethod('BindContract', $all).Invoke($null, $weeklyArgs)
$commissionQuery = $bridge.GetMethod('BindCommission', $all).Invoke($null, $caravanArgs)
if (($null -ne $weekly) -ne ($null -ne $contractQuery) -or
    ($null -ne $caravan) -ne ($null -ne $commissionQuery)) { throw 'Public API binding mismatch' }
$checkpoint = Get-Content -Raw -LiteralPath $CopiedSave | ConvertFrom-Json
$state = New-SceneObject ([SuperFantasyKingdom.GameManager])
$data = New-SceneObject ([SuperFantasyKingdom.GameDataManager])
$clock = New-SceneObject ([SuperFantasyKingdom.DaytimeManager])
$null = New-SceneObject ([SuperFantasyKingdom.RaceManager])
$null = New-SceneObject ([SuperFantasyKingdom.OverlayManager])
Set-Field $state 'm_IsGameLoaded' $true
Set-Field $state 'm_IsGameRunning' $true
Set-Field $data 'm_RunID' $checkpoint.runID
Set-Field $clock 'm_CurrentDay' 16
$schedule = [Activator]::CreateInstance($calendar.GetType('VisitorCalendar.Schedule'), $true)
Set-Field $schedule Today 16
Set-Field $schedule LastDay 28
$actor = [SuperFantasyKingdom.Actors]::Caravan
$known = $schedule.GetType().GetField('Known', $all).GetValue($schedule)
$null = $known.Add($actor)
$dates = $schedule.GetType().GetField('FixedDays', $all).GetValue($schedule)
$dates[$actor] = 21
if ($weekly) {
    $contract = [Activator]::CreateInstance($weekly.GetType('WeeklyContracts.Contract'), $true)
    $contract.run = $checkpoint.runID
    $contract.stage = 'Active'
    $contract.offeredDay = 15
    $contract.deadline = 21
    $weekly.GetType('WeeklyContracts.Plugin').GetField('Current', $all).SetValue($null, $contract)
}
if ($caravan) {
    $plugin = $caravan.GetType('CaravanCommissions.Plugin')
    $book = [Activator]::CreateInstance($caravan.GetType('CaravanCommissions.OrderBook'), $true)
    $book.items = [string[]]@('Burn', 'Hammer', 'Rage')
    $plugin.GetMethod('Reset', $all).Invoke($null, @($book))
    $observer = $calendar.GetType('VisitorCalendar.NativeBookings').GetMethod('CommissionObserver', $all)
    foreach ($entry in @('Arrival:Prefix','ScheduledArrival:Prefix','ScheduledArrival:Finalizer')) {
        $type, $method = $entry.Split(':')
        $target = if ($type -eq 'Arrival') { [SuperFantasyKingdom.Guest] } else { [SuperFantasyKingdom.GuestCaravan] }
        $hook = $plugin.GetNestedType($type,$all).GetMethod($method,$all)
        if (!$observer.Invoke($null,@($target,'OnLoad','gkj.sfk.caravancommissions',$hook)) -or
            $observer.Invoke($null,@($target,'OnLoad','unknown.provider',$hook))) { throw "Observer filter: $entry" }
    }
}
$reader = $calendar.GetType('VisitorCalendar.CalendarNotice').GetMethod('Read',$all)
function Check-Entries($expected) {
    $notices = $reader.Invoke($null, @($schedule,$contractQuery,$commissionQuery,[Action[Exception]]{param($e) throw $e}))
    $actual = foreach ($n in $notices) {
        $fields = $n.GetType()
        $title = $fields.GetField('Title',$all).GetValue($n)
        $day = $fields.GetField('Day',$all).GetValue($n)
        $detail = $fields.GetField('Detail',$all).GetValue($n)
        if ($title -eq 'Delivery Due' -and $detail -ne "Bread for the Watch $([char]0xb7) 6 Bread") { throw "Wrong contract terms: $detail" }
        "$title@$day"
    }
    if (($actual -join ',') -ne ($expected -join ',')) { throw "Expected [$expected], got [$actual]" }
}
$due = @()
$dated = @()
$undated = @()
if ($weekly) { $due += 'Delivery Due@21' }
if ($caravan) { $dated += 'Commission Forecast@21'; $undated += 'Next Caravan@' }
Check-Entries ($due + $dated)
$dates[$actor] = 16 # Today's scheduled visit has already been spent.
Check-Entries ($due + $undated)
if ($caravan) {
    # Bind a distinct native item picker to the provider, as a minimized visit would be.
    $guest = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([SuperFantasyKingdom.GuestCaravan])
    $screen = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([SuperFantasyKingdom.UI.UIOverlayMerchant])
    $visit = $plugin.GetMethod('Arrive', $all).Invoke($null, @($guest,16))
    $screens = $plugin.GetField('screens', $all).GetValue($null)
    $screens.Add($screen, $visit)
    $overlay = [SuperFantasyKingdom.OverlayManager]::Instance
    Set-Field $overlay 'm_Backup' $screen
    Set-Field $overlay 'm_MinimizedOverlay' $true
    Check-Entries ($due + 'Commission Delivery@16')
    if (!$book.GetType().GetMethod('Finish', $all).Invoke($book,@(16,1,$false))) { throw 'Defer fixture failed' }
    Check-Entries ($due + $undated)
}
if ($weekly) { $contract.stage = 'Earned' }
Check-Entries $undated
if ($caravan) { $book.items = [string[]]@() }
Check-Entries @()
Write-Output "PASS actual DLL queries, fixture kingdom and orders: Weekly Contracts=$([bool]$weekly), Caravan Commissions=$([bool]$caravan)"
