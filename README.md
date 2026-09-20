# Rion Win 11

A Windows 11 utility for GPU controls, driver preparation, BIOS profiles, startup apps, power settings and reversible system tweaks.

## Download

| Portable | Installer |
| --- | --- |
| [Download portable EXE](https://github.com/riontweaks/rion-win-11/releases/download/Files/Rion-Win11-Portable.exe) | [Download installer EXE](https://github.com/riontweaks/rion-win-11/releases/download/Files/Rion-Win11-Setup.exe) |
| Run without installing. | Offline setup with shortcuts and an uninstaller. |

Windows x64. Both downloads include .NET. [Release notes and checksums](https://github.com/riontweaks/rion-win-11/releases/tag/Files).

GPU edits are reviewed before applying, then verified with a timed Keep/Revert confirmation. BIOS Settings, Fans and Profiles use saved scans and reviewed changes; firmware imports prompt for a restart after completion. Unverified NVIDIA mappings and ambiguous firmware fan curves remain unavailable.

## Source and licenses

[Download the source matching this release](https://github.com/riontweaks/rion-win-11/raw/64bbef2fdfe441706a9954baead991792b1b7ad5/Rion-Win-11-source-20260919-202416.zip). The archive contains all 816 application, test, build-tool and public documentation files verified against the packaged source manifest. Extract it and follow its README to build with the .NET 8 SDK on Windows. GitHub's automatic archives for the older `Files` tag are not this corresponding source.

Source archive SHA-256: `1FCB56E655001905E5809BCBD5B125D06E4E981FA26F54903DBD4794B8602F25`.

The combined application uses GPL-3.0; third-party licenses and copyright notices are preserved in the source archive and **About → Licenses and notices** inside the app. Releases are unsigned. Automated tests use fake providers and offline BIOS exports; physical hardware writes and clean-machine installation still require validation.
