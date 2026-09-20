# Process reduction

Auto-Optimize includes an opt-in **Process reduction** section. In the scan review,
expand its card to choose **Keep available** features. The three collapsible groups cover
devices/connections, reporting/background services, and apps/vendor helpers. All keep choices
default to on. Uncheck only capabilities you want to give up; the preview explains the affected
entries and blocked dependencies. Applying rescans with the same choices and does not stop
services or close applications. Restart/sign out for service changes.

Printing (including Print to PDF/virtual printers) and WIA scanning are independent. Phone/messaging
and mail/calendar/contact sync are independent. Optional Bluetooth and connected-device groups
do not weaken general pairing, input, audio or networking protection. Opting out of Bluetooth
can remove Bluetooth keyboard/mouse/audio access after restart; arrange wired input first.

Additional choices: Windows diagnostic reporting (DiagTrack), device-management push routing,
crash/problem reporting, Windows troubleshooting, compatibility assistance, moved-file link
tracking, offline maps, retail demo, remote registry administration, forwarded-event collection,
Insider support, developer diagnostics, verified ASUS update checks and Logitech LampArray lighting.
Reviewed vendor updaters are now kept by default and require their own opt-out (Xbox updaters
also honor Keep Xbox). OneDrive, MIDI, Edge background, Widgets and Search highlights remain
independent choices. Unsupported/unverified vendor variants are retained for review.

This does not run the legacy TelemetryTasksRunner or claim to disable every application's telemetry.
CryptSvc, Windows security/update infrastructure, licensing, backup, game runtimes and general
device pairing remain protected. Diagnostics are not all telemetry, and services already stopped
or disabled are not counted as measured process savings. Optional services with dependents remain
blocked even when another feature is unchecked. Existing disabled services are not repaired.

System-service changes apply machine-wide. Per-user templates are explicitly identified using
service-type flags; their Start values are journaled for future sign-ins, while current instances
are handled separately. Templates and instances get independent history entries. Template dependency
checks include registered references and current instances; failures retain the entry. Startup app
changes retain their existing current-user or machine scope. Unchecking a feature does not silently
edit other users' profile hives. Restore entries in Startup history, then sign out/restart.

The preset uses the shared nearest-RAM-tier `SvcHostSplitThresholdInKB` calculation. It disables
reviewed background services and recognized, signature-verified optional startup applications.
Other apps, unresolved startup shortcuts and feature-dependent services are retained for review;
they are not automatically assumed optional. Startup defaults to a simple Apps view. Background
extras and Updaters have their own views; each page shows at most 12 entries, with friendly names,
plain-language guidance and expandable technical details. The complete inventory is retained in
**All entries (advanced)**. The suggested-disable button only affects suggestions on the visible
page. Startup controls support individual manual changes.
RunOnce/setup entries and tasks other than boot/logon app launches remain unchanged.

Exception: explicitly recognized updater tasks (such as the signed Adobe Acrobat Update Task)
can be included even with other schedules. Adobe and Google updater rules require matching
service/task identity, executable and verified publisher. OEM Realtek/Epic/Xbox update-only
service variants require a matching verified publisher plus update-specific service name,
executable and description. These rules do not assert such a service exists on every PC.
Adobe, browser and vendor update checks can stop; manual updating is explained per entry.
Realtek audio, Gaming Services, Epic Online Services, anti-cheat (including Vanguard) and Adobe
licensing are protected. Microsoft Store and Windows Update remain protected; they are not
treated as Xbox updaters. Xbox update-only entries honor the Keep Xbox choice.

Networking, generic pairing, audio/microphone, Search, VSS/Shadow Copy, backup and restore-point
dependencies, security, updates, sign-in and core Windows services are excluded. Per-user
service instances inherit the protection of their template. Service dependencies are checked
at preview and again at apply; candidates with dependents are retained, even if a related
optional feature was unchecked. An existing disabled service is not automatically repaired.

The run attempts the existing System Restore point creation. A failure is reported; it does
not imply that a restore point exists. App/service originals are flushed to Startup history
before each write, and grouping has a registry backup persisted before applying. Undo
apps/services from Startup history; restore grouping from its registry backup. The preset
does not stop services or terminate applications. Restart to apply service changes.

Fewer processes are not a performance guarantee. Grouping trades away service isolation.
No changes are made to `SvcHostSplitDisable`, drivers, or arbitrary scheduled tasks by this preset.

References: [Microsoft service-host grouping](https://learn.microsoft.com/en-us/windows/application-management/svchost-service-refactoring)
and [Microsoft service descriptions](https://learn.microsoft.com/en-us/windows/iot/iot-enterprise/optimize/services).
The latter is guidance for fixed-purpose IoT devices, not a blanket desktop disable list.

Validation: startup/protection NUnit tests, shared threshold tests, solution build, and
`tests/ProcessPreview` which renders the actual WPF picker. Its optional `--scan` mode performs
a read-only live preview and verifies protected services, conditional choices, optional-preview
execution eligibility, repeat selection, startup filtering and pagination.
It does not run optimization or modify service/startup state.

## Recommended tweaks

`GeneralTweakCatalog` is the shared source for Tweaks and Auto-Optimize. The 17 recommended
privacy/suggestion/interface tweaks have visible badges in Tweaks; **Apply recommended** and
Auto-Optimize's **Recommended tweaks** use that exact set. Evidence grades alone do not make a
tweak recommended. Hardware, security, timing, recording, sync, power and manual policy choices
remain individual. RAM-tier service grouping is applied through Process reduction, not through
the recommended-tweaks section. The section no longer runs unrelated scheduled-task changes.
