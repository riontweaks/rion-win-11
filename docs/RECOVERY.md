# Source recovery and feature verification

The surviving executable was preserved. Its SHA-256 is
`33D6C4CA010A83C59BDE416F96DDA29CE993AF943A3EBBFD47017069F8E1949C`.
The .NET single-file bundle contained 235 payloads. The canonical source under
`src/` combines recovered workspace snapshots with ILSpy reconstruction of
missing C# and WPF resources. Buildable source is available; reconstruction does
not recover original comments or prove complete behavioral parity. Private
recovery inventories and original payloads remain outside the public source tree.

## BIOS and utilities

Utilities is a top-level page built from the existing Windows utilities row
styles and download/run implementation. It retains the original catalog and
adds the nine imported launchers and 7-Zip. Tools are categorized and searchable.
CoreCycler has one selector for PRIME95, YCRUNCHER, and YCRUNCHER_OLD. Selection
saves the configuration after checking for a running CoreCycler instance and
external file changes. Other configuration text is preserved and the previous
file is retained as a backup. No stress test starts when the selector changes.

BIOS identifies the current PC's motherboard manufacturer, model, and firmware
version using read-only CIM queries. These identify this PC, not the provenance
of an opened export. Exported settings appear in a virtualized list with inline
choices/numeric controls and per-row details. Unsupported, ambiguous, and
protected fields remain read-only. Edits are staged until the separate reviewed
import action. Saved exports are explicitly distinguished from fresh scans.

The firmware integration retains original exports, validates the supplied batch
files and driver binaries, and calls Export.bat/Import.bat through ShellRunner.
No firmware import/export or driver loading has been executed during development
verification. Automatic AMD/Intel recommendations are not implemented: a
verified board/firmware identity map is still required, including distinguishing
AMD PBO entries from similarly named Ai Tweaker entries. The current UI explains
this limitation. Motherboard detection alone does not supply that mapping.

## Verification entry points

`src/RionSuite/tests/RecoveryFeatures/RecoveryFeatures.csproj` verifies the RAM
threshold table, exact CoreCycler backups, configuration conflict rejection,
BIOS no-op encoding preservation, edits, bounds, duplicate identities, profiles,
and counter/driver matching helpers. Optional `--fixture <nvram.txt>` validates a
saved export without accessing firmware. `--archive <Rion2.zip>` checks the
pinned archive, extracts only into a temporary test directory, and verifies
launch paths without executing tools. `--render <directory> [nvram.txt]` renders
native WPF pages at full and compact sizes and uses read-only motherboard
detection. It does not start the application startup downloads.

These checks do not establish firmware compatibility, hardware stability,
performance gains, driver-install success, or completion of every feature in the
larger enhancement request. Network Benchmark now includes active HTTPS upload
and download with payload limits, cancellation, loaded/unloaded latency,
connection diagnostics, local reports and repeated-run descriptive comparisons.
Its engine is tested against a real loopback HTTP server. No public-Internet
capacity measurement or automatic network-setting trial was performed.

BIOS now includes two-file comparison, hexadecimal/signed/string editing,
explicit exported range/step validation, a 96-entry source-draft review and live
operation output. AMI detection gates native execution; saved/demo exports cannot
write firmware. Active Import.bat is never forcibly terminated on a timeout.
The source draft remains distinct from a verified AMD/Intel firmware preset.
Full coverage and remaining work: [implementation record](IMPLEMENTATION.md).
