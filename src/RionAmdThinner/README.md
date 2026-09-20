# Rion AMD Thinner

A Windows + AMD Radeon tweaking utility. Slim the Adrenalin installer before it runs,
toggle Windows registry tweaks, manage startup items and services, and turn off Radeon
overlay / ReLive / telemetry noise — with a restore point and per-change backups so
anything can be undone.

**Fork of [RadeonSoftwareSlimmer](https://github.com/GSDragoon/RadeonSoftwareSlimmer)**
by Greg Seaton, used under the GPL-3.0 licence (see `LICENSE` and `NOTICE`). The
pre-install installer-slimming and the AMD post-install engine (services, scheduled
tasks, host processes, installed entries, temp files) come from that project.

## What it does

| Section | |
|---|---|
| **AMD Installer** | Download the latest auto-detect installer from amd.com, or pick a full Adrenalin package; extract it; uncheck packages / scheduled tasks / display components; then **launch** it or **save the slimmed copy** elsewhere. Optionally wait for the install to finish and auto-apply the ticked Radeon tweaks. Same JSON-manifest / task-XML technique as Radeon Software Slimmer. |
| **Windows Tweaks** | ~22 curated, reversible registry tweaks — window/taskbar animations, "suggested content" ads, advertising ID, tailored experiences, web results in Search. **Run recommended settings** stages only the low-risk set; an **ⓘ** button explains each. |
| **AMD Adrenalin** | Radeon overlay / ReLive / DVR / capture hotkey and toggle tweaks, plus lower-level driver tweaks (ULPS, TdrDelay, MPO / `OverlayTestMode`, shader cache, flip queue, and clearly-flagged GCN-era experimental ones). Below it: the full RSS post-install engine. |
| **Startup & Services** | Logon startup items (disabled the Task-Manager way, never deleted) and a curated set of Windows services. |
| **Overview / Activity Log / About** | Machine summary + "revert last apply", the running log, credits. |

Every staged section: flip toggles -> **Apply**. Optional **restore point first**;
previous values are written to `%LOCALAPPDATA%\RionAmdThinner\backups\<timestamp>.json`
for **Revert last apply**.

## Safety

**NOT** owned, supported or endorsed by Advanced Micro Devices, Inc. (AMD) or Microsoft.
Low-level driver and service changes carry real risk. Use the restore-point option,
read each tweak's note, and understand that the "experimental" tier can raise
temperatures and cause instability. **Use at your own risk.**

## Build

.NET 8 SDK, Windows 10/11 x64.

```
dotnet build RadeonSoftwareSlimmer.sln -c Debug
dotnet run  --project src/RadeonSoftwareSlimmer
dotnet test RadeonSoftwareSlimmer.sln
```

Publish: `pwsh build/publish.ps1` (or `dotnet publish -c Release`) ->
`artifacts/bin/RadeonSoftwareSlimmer/.../RionAmdThinner.exe`.

Runs as `asInvoker`; writing HKLM keys, service start-modes and scheduled-task state
needs elevation, so the app shows a **Restart as administrator** button when it isn't.

## Layout

| Path | Role |
|---|---|
| `MainWindow.xaml` + `ViewModels/ShellViewModel.cs` | left-rail shell |
| `ViewModels/Sections/*` | one `SectionBase` per rail entry |
| `Models/RegistryTweak.cs` + `Services/TweakCatalog.cs` / `AmdTweakCatalog.cs` / `AmdGpuTweakCatalog.cs` | the tweak catalogs |
| `Services/StartupInventory.cs` / `WindowsServiceCatalog.cs` / `AmdInventory.cs` | inventories |
| `Services/DriverDownloadService.cs` | amd.com auto-detect installer download |
| `Services/BackupStore.cs` / `RestorePoint.cs` | undo |
| `Models/PreInstall/*` + `Models/PostInstall/*` + `src/Shared` (7-Zip) | RadeonSoftwareSlimmer engine, largely unchanged |
| `Themes/Dark.xaml` | hand-rolled Fluent (WinUI 3) dark theme |

## Third-party

.NET, [7-Zip](https://www.7-zip.org/) (`src/Shared/7-Zip`, decompresses the installer),
[Json.NET](https://www.newtonsoft.com/json), [Task Scheduler Managed Wrapper](https://github.com/dahall/taskscheduler),
[System.IO.Abstractions](https://github.com/TestableIO/System.IO.Abstractions).
Upstream: [RadeonSoftwareSlimmer](https://github.com/GSDragoon/RadeonSoftwareSlimmer).
