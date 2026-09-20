# Third-party notices

## CPU vendor identification images

The AMD and Intel images in `Assets/CpuVendors` were supplied by the project
owner for CPU identification in the BIOS header. AMD and Intel names and logos
remain trademarks of their respective owners; the application's license does
not grant trademark rights or imply vendor endorsement. These images are
embedded unchanged and selected using the detected CPU manufacturer.

Rion Win 11 incorporates the following material. This inventory is a release-preparation record, not a claim that every provenance question is resolved.

## Radeon Software Slimmer

Copyright Greg Seaton (GSDragoon). GPL-3.0. The fork and shared engines retain their license and NOTICE in `src/RionAmdThinner/`. Original project: https://github.com/GSDragoon/RadeonSoftwareSlimmer. The combined application is distributed under GPL-3.0, subject to the remaining provenance review below. See `LICENSE` and `licenses/RadeonSoftwareSlimmer-NOTICE.txt`.

## ALDX

Copyright 2026 Rion. The existing MIT license remains at `src/ALDX/LICENSE` and `licenses/ALDX-MIT.txt`. AMD's ADLX runtime is supplied by the installed driver, not relicensed as Rion code. SDK reference downloads are not included in this source tree.

## 7-Zip

Copyright 1999–2026 Igor Pavlov. The embedded `7z.exe` and `7z.dll` are retained because the installer engines require them. Their LGPL, BSD and unRAR terms remain in `src/RionAmdThinner/src/Shared/7-Zip/License.txt`. Source and licensing: https://www.7-zip.org/ and https://www.7-zip.org/license.txt. These binaries are not Rion-authored code.

Binary packages include the preserved ALDX MIT and Radeon Software Slimmer notices in `licenses/`, plus the 7-Zip terms at `licenses/7-Zip-LICENSE.txt`. The standalone executable also embeds these texts; open About → Licenses and notices. Folder distributions retain the accompanying files. This preserves the existing texts; it does not resolve the remaining provenance or dependency review.

## Rion Power Settings

The project owner states that this module was independently authored for Rion. That statement has not been independently verified; the implementation's provenance review remains open. The module calls Windows power-management APIs, whose presence alone does not establish source authorship. Preserve applicable rights and notices if third-party contributions are identified.

## NuGet and .NET

Dependencies are declared in project files, including Microsoft/.NET libraries, Newtonsoft.Json, System.IO.Abstractions and TaskScheduler. Preserve applicable package notices in binary distributions. Run `dotnet list './Rion Win 11.sln' package --include-transitive` for the resolved dependency inventory. A complete dependency-license review remains a release step.

## Vendor trademarks

AMD, NVIDIA, Microsoft and Windows are their respective owners' trademarks. Rion Win 11 is not an official product of those vendors.

The NVIDIA integration uses constants and ABI declarations from NVIDIA's public NVAPI headers. Their MIT notice is preserved in [licenses/NVIDIA-NVAPI.txt](licenses/NVIDIA-NVAPI.txt). The installed driver supplies the runtime library; it is not redistributed.

References checked during preparation: [GNU combined-program guidance](https://www.gnu.org/licenses/gpl-faq.en.html#GPLInProprietarySystem), [7-Zip redistribution FAQ](https://www.7-zip.org/faq.html).

## Installer brand icons

The installer includes 23 monochrome vector logos from Simple Icons 16.30.0 (https://github.com/simple-icons/simple-icons). CC0 project license and disclaimer are preserved in licenses/Simple-Icons-LICENSE.md and licenses/Simple-Icons-DISCLAIMER.md. Individual source, license and guideline metadata is retained in InstallerBrandIcons.sources.json. Logos identify the software offered for installation; trademarks remain with their owners. Installed Windows/app artwork is read locally and is not redistributed.


## September 2026 dependency evidence

Bundled 7-Zip is version 26.03. Corresponding upstream source and GitHub release digests are preserved under licenses/7-Zip; the installer SHA-256 was verified against the official release metadata before extraction. NuGet package metadata and available license texts are in licenses/NuGet. Build-time analyzers are distinguished by their project PrivateAssets metadata; this inventory does not relicense third-party code. See docs/SECURITY.md for security changes and the unresolved power-engine provenance.

## Embedded interface fonts

Barlow by the Barlow Project Authors (https://github.com/jpt/barlow) and Source Sans 3 by Adobe (https://github.com/adobe-fonts/source-sans) are distributed under SIL OFL 1.1. Original notices are in `licenses/fonts/`.
# NSIS installer

The optional single-EXE installer is built with NSIS 3.12. Copyright 1999–2026
NSIS contributors. Licensing for the installer engine, Modern UI and compression
components is preserved in `licenses/NSIS-COPYING.txt`. Compiler/source:
https://nsis.sourceforge.io/ . Rion application licensing remains unchanged.
