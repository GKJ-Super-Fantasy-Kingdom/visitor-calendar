# Contributing to Visitor Calendar

## Report a bug

Open a [GitHub issue](https://github.com/GKJ-Super-Fantasy-Kingdom/visitor-calendar/issues) with the mod, game, and BepInEx versions, your kingdom and day, and the steps to reproduce the problem. Include the relevant log excerpt. For interface or input issues, include the UI scale and whether you used a controller or keyboard and mouse.

## Submit a change

Fork the repository, create a branch, and follow the build and packaging instructions below. Use the assemblies from your own game installation and BepInEx profile as build references. Keep game binaries, save files, and local build output out of commits.

Describe the player-visible change in your pull request. List the checks you ran and, where relevant, which controller actions and UI scales you tested.

## Commit messages

Use [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) for each commit. Start the subject with a type, an optional scope, and a short description. Use an exclamation mark before the colon for a breaking change.

Examples include `feat(ui): show controller prompts`, `fix(save): restore the current day`, and `chore: update build tooling`.

Accepted types are `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`. Use the same format for pull request titles so GitHub can use the title as the merge or squash commit subject.

## Build and package

From the repository root with the .NET SDK and Python 3 installed:

```powershell
dotnet build VisitorCalendar.csproj -c Release
dotnet run --project tests/CalendarChecks.csproj -c Release
python package.py
```

The projects default to the local Steam game and the Daybook Dev profile for build references. Override `GameDir` and `ProfileDir` with MSBuild properties for another installation. Packaging writes `dist/VisitorCalendar-0.1.2.zip`.

To compare the calendar with specific companion builds, pass the VisitorCalendar, WeeklyHermit and DailyCaravan DLL paths after `--` on the test command. Append the KingdomCharters DLL path to exercise the Merchant charter's first-visit policy and its interaction with Daily Caravan.
