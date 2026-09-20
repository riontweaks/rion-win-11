# Dependency notices

`inventory.json` records 16 resolved NuGet packages used by the production projects, including build analyzers. Each listed package now has its upstream license text in a matching name-version directory. This is an inventory of resolved packages, not a claim that every listed DLL ships in the EXE. The self-contained .NET 8.0.31 runtime license directories are included separately.

`upstream-license-sources.json` records exact upstream commits, license-file URLs, Git blob hashes and SHA-256 hashes for the six package notices retrieved during the follow-up review. Five packages use the repository commit in their installed NuGet metadata; the analyzer package has no commit in its metadata, so its upstream v2022.0.0 tag was resolved and recorded. Original license text and copyright dates are preserved, even where package metadata gives different years.

The .NET runtime's THIRD-PARTY-NOTICES.TXT is copied unchanged from the resolved microsoft.netcore.app.runtime.win-x64 8.0.31 package. Keep it with the runtime LICENSE.TXT. These notices and metadata do not change third-party ownership or resolve the Power Settings Explorer permission gap described in the root THIRD_PARTY_NOTICES.md.

