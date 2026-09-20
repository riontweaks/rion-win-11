# Release security and scan evidence

Antivirus results describe one exact SHA-256 at a particular time. A successful build, a signature, open source, and an undetected result are different forms of evidence; none guarantees absence of malicious behavior or future detections.

## September 2026 hardening

The release excludes 60 imported registry controls that modified Defender/SmartScreen, process mitigations, LSA protection, VBS/DMA protection and GPU isolation. Five security-related BCD controls and the VBS/HVCI disable operation are retired: new applications fail; inspection and saved-state recovery remain. Existing registry history remains available independently of catalog membership. No Windows security settings are automatically changed by this release work.

The BCD state engine itself rejects retired Apply calls before inspection, journal creation or writes, including calls made without the catalog. The former VBS disabling implementation has been removed from the production assembly. Tests reconstruct old recovery records using an in-memory registry fixture; retaining historical recovery does not require shipping the retired Apply implementation.

The embedded 7-Zip pair is upgraded from 24.09 to 26.03. Its upstream source archive and release digests are in licenses/7-Zip. Cached EXE and DLL bytes are verified against the embedded resources on every request, with separate content-versioned directories and rejected links/junctions. Extraction callers retain an execution lease: verified file handles deny writes/deletion, and ancestor directory handles prevent the path from being exchanged while in use. Verification uses the same file handles held during extraction. Tests cover same-length substitutions, file/directory replacement attempts, concurrent readers, handle release, and extraction of an inert archive. This is protection against ordinary cache substitution, not a security boundary against an administrator or kernel compromise.

The shared PowerShell runner no longer requests ExecutionPolicy Bypass. Scripts now travel as one plain `-Command` argument, quoted by the runtime, so the child process receives the same text that appears in source. Execution policy does not apply to `-Command`, so no policy override is needed. Only a script too long for a command line falls back to UTF-16 Base64 transport, which no current caller reaches. Neither form disables AMSI: PowerShell submits the script for antimalware inspection either way. Restore-point creation also stops requesting a policy override and fails if system PowerShell is missing instead of searching PATH.

The network settings module previously shipped its script gzip-compressed and Base64-encoded, reconstructed in the child by `[scriptblock]::Create`. That is indistinguishable from a staged-payload loader on inspection, and it is now removed: the script is sent as readable text inside a script block. This was done because the encoded form was unreadable to a reviewer and to a scanner alike, not to influence any particular verdict.

`NetworkSettings.ps1` is kept ASCII-only. It is UTF-8 without a byte-order mark, and Windows PowerShell 5.1 decodes such a file as the system ANSI code page; a U+2013 en-dash in it decoded to a smart quote that PowerShell read as a string delimiter, so the script would not parse when invoked by path. Keep non-ASCII characters out of that file, or add a byte-order mark.

## Recorded scan results

One row per scanned build. A result describes those exact bytes at that moment and does not carry
forward to a rebuild, because every rebuild and every signature changes the hash.

| Scanned (UTC) | SHA-256 | Signed | Service | Result | Detecting engines |
| --- | --- | --- | --- | --- | --- |
| 2026-09-09 | `575d8ea346bbbb67a3f44d972053895cd146b06266faad2a9b08a8e4f483b1d6` | No | VirusTotal | 1/69 | Bkav Pro `W32.Malware.8C310B8F` |
| 2026-09-10 | `bd7ca100e6865d0b8164fe2fcbe1274379f644d61e7839b4c56e2dfa5abe3e1a` | No | VirusTotal | 2/68 | Bkav Pro `W32.Malware.E2C93E8C`, Zillya `Trojan.Agent.Win32.4629592` |
| 2026-09-10 | `575d8ea346bbbb67a3f44d972053895cd146b06266faad2a9b08a8e4f483b1d6` (re-analysis) | No | VirusTotal | 2/67 | Bkav Pro `W32.Malware.8C310B8F`, Zillya `Trojan.Agent.Win32.4629592` |

Reports:
- https://www.virustotal.com/gui/file/575d8ea346bbbb67a3f44d972053895cd146b06266faad2a9b08a8e4f483b1d6
- https://www.virustotal.com/gui/file/bd7ca100e6865d0b8164fe2fcbe1274379f644d61e7839b4c56e2dfa5abe3e1a

Notes on these scans:

- The third row is a re-analysis of the first row's unchanged bytes. It is recorded because it
  isolates a cause: the same file scored 1/69 and then 2/67, gaining exactly the Zillya detection
  that also appears on the newer build. The added detection came from a vendor database update, not
  from any source change between the two builds. Re-analyse the previous hash before concluding
  that a code change caused a new detection. The exact vendor-side reason is not exposed by this comparison.
- Acronis (Static ML) flagged the first build on its initial analysis and reports Undetected on
  every later analysis of both builds.
- Bkav's label differs between files: the same engine reports `W32.Malware.8C310B8F` for one
  build and `W32.Malware.E2C93E8C` for the other. Zillya's `Trojan.Agent.Win32.4629592` is likewise a
  numeric identifier. These labels do not reveal the matched bytes, detection method or malicious behavior.
- The first build is not reproducible from the current tree. Submit the hash of the build actually
  being released.

### Packaging characteristics to investigate

These characteristics can be relevant to investigation, but the available reports do not establish
which, if any, caused Bkav or Zillya to flag the application:

- The release is unsigned. Signing establishes publisher identity and integrity, but does not
  guarantee removal of an antivirus verdict. Download prevalence has not been independently measured.
- Single-file self-contained publishing produces a small host with a ~141 MB overlay. TrID reports
  it as "PECompact compressed (generic)"; direct bundle parsing found uncompressed .NET entries.
- The manifest requests administrator rights, and the application writes to the registry, services
  and network configuration by design.
- 7-Zip is embedded and materialized to a cache directory before use, which resembles a dropper to
  behavioral heuristics. `SevenZip.Acquire()` verifies both components on every request.
- Portable utilities are downloaded and launched. Each is pinned by SHA-256 or verified by
  Authenticode publisher before it runs; see `PortableToolDownloader`.

### False-positive submissions

Submit the exact hash with evidence, and record the outcome here:

- Bkav: https://www.bkav.com/report-false-positive (or `scan@bkav.com.vn`)
- Microsoft, if Defender ever flags a build: https://www.microsoft.com/en-us/wdsi/filesubmission

## Verification workflow

1. Build from a fixed source snapshot with tools/Build.ps1 and run tools/Test.ps1 plus ReleaseTrustTests and RecommendedTweakTests.
2. Audit direct and transitive NuGet dependencies for every production project. Keep the full JSON output and timestamp outside public source. A zero-advisory result is not a malware scan.
3. Publish with tools/Publish.ps1 and export the unchanged source with tools/Export-Source.ps1. Compare the source inventory to release-manifest.json.
4. Hash the final distributable, verify Authenticode status, and submit that exact file to the authorized scanning services. Record each vendor, label, report URL and date; distinguish undetected, unsupported, timeout, failed and pending analyses.
5. Sign only using the legitimate publisher's certificate. `tools/Publish.ps1` signs the packaged executable before it hashes it, so the manifest and `SHA256SUMS.txt` already describe the signed bytes; set `RION_SIGN_CERT` or `RION_SIGN_THUMBPRINT` (with `RION_SIGN_PASSWORD` and `RION_SIGN_TIMESTAMP` as needed) outside the repository. Publishing without those variables produces an unsigned release and says so. Do not add obfuscators, antivirus exclusions or security bypasses to change a scanner result.
6. Submit a suspected false positive to the detecting vendor with evidence. VirusTotal aggregates vendor verdicts and cannot clear them itself.

## Open-source release status

Rion's combined code carries GPL-3.0 with upstream notices preserved. Bundled 7-Zip source is provided; resolved NuGet metadata and available license texts are included under licenses/NuGet. Upstream power-engine permission remains unresolved: the module documents a port from a decompiled 2017 program. Do not describe the whole release as fully cleared for public redistribution until that provenance is resolved or the affected implementation is replaced with appropriately licensed code. No public repository destination or publisher signing certificate is configured by this change.

## References

- https://docs.virustotal.com/docs/false-positive
- https://docs.virustotal.com/docs/how-it-works
- https://learn.microsoft.com/en-us/defender-xdr/developer-faq
- https://www.7-zip.org/history.txt
- https://www.7-zip.org/download.html
- https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_powershell_exe?view=powershell-5.1
- https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew
# Bundled runtime servicing

The September 14 investigation confirmed that the September 13 Desktop EXE (SHA-256
`52AA6E268A20F76CD65359D14F26BD6930D1DBA2ED2E51F7687A473C4CE6BD86`) regressed to
SDK 8.0.424 and embedded NETCore/WindowsDesktop 8.0.30. The SDK minimum is restored to
8.0.425 with patch-only roll-forward. Publish now rejects missing, ambiguous or older
NETCore/WindowsDesktop runtime configuration before promoting output. This corrects
a security servicing regression; its relationship to the antivirus detections is unproven.

The same review replaced filename-only AMD/NVIDIA native loading with absolute System32
paths and System32 dependency search. This closes the reviewed application-directory/PATH
substitution route. GPU drivers installed outside the standard system location are not
loaded through a fallback. No malicious substituted DLL was found beside the inspected EXE.

The build requires .NET SDK 8.0.425 or a compatible newer .NET 8 SDK. SDK 8.0.425 includes the .NET 8.0.31 security update released on September 8, 2026. A self-contained executable carries its runtime: updating Windows or the separately installed .NET runtime does not replace those embedded bytes. Rebuild and redistribute after relevant runtime security updates, and verify the resolved runtime version in the published dependency manifest. See Microsoft's [8.0.31 release notes](https://github.com/dotnet/core/blob/main/release-notes/8.0/8.0.31/8.0.31.md). Updating the runtime does not establish an antivirus verdict.
