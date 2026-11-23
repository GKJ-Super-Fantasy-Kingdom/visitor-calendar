# Visitor Calendar

The day display shows today's known visitors with their in-game portraits. Open the game's Calendar to see appointments on the day grid. Select a portrait's day to inspect the visitor. When two visitors are booked together, use **Previous** and **Next** inside the appointment. After taking a visitor's offer, the day display marks that visitor **Claimed today**. Inspecting, rerolling and declining leave the claim clear.

The calendar uses the active kingdom's discoveries, invitations and challenge dates. **Weekly Hermit** supplies its configured repeat interval and needs an eligible recruit. **Daily Caravan** supplies its configured first supplemental day and needs the native unlocks. Both additions apply to ordinary runs. The calendar merges invitations and scheduled visits for the same actor on the same day. Game portraits and localized visitor titles are loaded from the installed game.

## Local import

1. In Thunderstore Mod Manager or r2modman, select **Super Fantasy Kingdom** and open a dedicated test profile. Install the BepInEx dependencies listed in the manifest.
2. In the profile settings, choose **Import local mod**, then select `VisitorCalendar-0.1.0.zip`. Start the game with **Start modded** during your test slot.
3. In a kingdom, look for **Calendar** beneath the day display. The BepInEx log should contain `Visitor Calendar 0.1.0 ready.`

## Controller playtest

1. From kingdom play, press the game's **Calendar** action (default **D-pad Up**, or **K** on keyboard). The current day should receive focus. Clicking the day badge opens the same calendar.
2. Use the stick or arrow keys to move to a day with a known portrait. Press the displayed **Submit** binding or click the day. Verify the portrait, visitor name, day and status. With two appointments, move to **Next** or **Previous** and submit.
3. Press the game's **Back** action. Focus should return to the same day. Press Back again and verify the previous HUD selection or ordinary world controls resume. Repeat with a remapped Calendar binding and switch from mouse to gamepad during inspection.
4. Compare an unopened encounter with a discovered one. Take one visitor's offer and reopen the calendar. Only that visitor should gain **Claimed today**. Check again after Continue, a replayed dawn, and the next day.
5. Compare base game, Weekly Hermit, Daily Caravan and both together. With default settings, check day 16 in an ordinary kingdom where the Caravan is unlocked and the Hermit has an eligible recruit. Both should have individual portraits and claim states. Repeat with a changed interval, a changed Caravan start day, and an authored challenge.

Native claims follow the loaded day checkpoint. Same-day claim records, when applicable, live in the test profile's `BepInEx/config/VisitorCalendar` folder. Daily Caravan's own restored selection can supply its claimed status.

## Build and package

From the repository root with the .NET SDK and Python 3 installed:

```powershell
dotnet build VisitorCalendar.csproj -c Release
dotnet run --project tests/CalendarChecks.csproj -c Release
python package.py
```

The projects default to the local Steam game and the Daybook Dev profile for build references. Override `GameDir` and `ProfileDir` with MSBuild properties for another installation. Packaging writes `dist/VisitorCalendar-0.1.0.zip`.

To compare the calendar with specific companion builds, pass three DLL paths after `--` on the test command, in the order VisitorCalendar, WeeklyHermit, DailyCaravan.

## Contributing and license

Bug reports and pull requests are welcome. See the [contribution guide](CONTRIBUTING.md) for local testing and reporting instructions.

The original mod code is available under the [MIT License](LICENSE). Super Fantasy Kingdom, its game assets, and third-party dependencies retain their respective licenses and ownership.
