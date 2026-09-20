# GPU managers: implementation and verification

## Installer completion

AMD distinguishes the Adrenalin application from installer windows. Either a newly launched Adrenalin application after installation activity or installer closure leads to driver validation. Validation requires the matching PCI vendor, a vendor driver version/provider, and Windows device error code zero. Cancellation/closure without installation evidence does not automatically apply selected tweaks. The post-install path validates again before changing services. It waits for the installer instead of terminating its process tree, and advances when post-install work completes.

NVIDIA displays elapsed time and reads installer status/progress through UI Automation when the vendor window exposes it. Silent setup remains indeterminate when no actual percentage is available. NVIDIA app launch triggers a driver check; setup exit and driver validation gate completion. Post-install tools open after successful validation. No driver installer was executed during development validation.

## Recommended graphics profiles

Both Better Adrenaline and NVIDIA Controls now have a Recommended profiles sidebar entry. Balanced, Esports, Quality and Efficiency are app-curated starting points. Selecting a profile previews its values; Apply profile saves a snapshot and writes the supported global performance settings sequentially. It disables conflicting AMD features before enabling another mode, handles the Chill min/max transition, and verifies final driver readback. Failure stops the remaining writes and reports a partial result. Unsupported settings and out-of-range targets are skipped. Revert all uses the most recent saved snapshot. Display and overclocking changes remain individual controls.

The profiles share a consistent setting set per vendor, so returning from Efficiency to Balanced or Esports removes the previous preset's frame-rate limits. NVIDIA Balanced now has a real recommendation map. NVIDIA Esports uses the implemented On queue setting rather than claiming the unimplemented Ultra mapping works. No vendor-certified FPS improvement is claimed.

Profile definitions reference [AMD Chill/Anti-Lag guidance](https://www.amd.com/en/resources/support-articles/faqs/DH-033.html) and [NVIDIA's graphics-setting reference](https://www.nvidia.com/content/Control-Panel-Help/vLatest/en-us/mergedProjects/3D%20Settings/Manage_3D_Settings_%28reference%29.htm). Run `dotnet run --project RionSuite/tests/GpuProfiles/GpuProfiles.csproj` to check all four presets and repeated profile switching using injected mock providers only.

## NVIDIA installation workflow

Welcome → Select Driver → Analyze → Packages → Background → Review → Install → Post-Install → Finish.

Post-install tools contain the shared GPU monitor, graphics tweaks, display controls, installed-package management, and the existing startup/service engine filtered to NVIDIA. Package modification reopens package selection or driver selection when there is no extracted package. Driver package modification requires another installation; installed applications use their registered uninstall flow. Startup changes retain the existing engine's backups and undo controls.

## Native controls

Current routing and validation supersede the older summary below: see [GPU control coverage](../../../docs/GPU-CONTROL-COVERAGE.md). AMD now includes all five custom-colour controls and display selection. NVIDIA now includes display selection, topology-preserving scaling, output dynamic range and 28 additional graphics/DLSS settings. The old statement that GPU/integer scaling has no native path is no longer current. Full vendor-app parity is still not claimed.

- AMD: pixel-format and HDCP writes, five real Vari-Bright presets, and colour-temperature range/read/write now join existing display controls. Display edits use a 15-second keep-or-revert dialog. Colour temperature is a numeric white-point setting; Vari-Bright is a preset selector rather than an invented percentage.
- NVIDIA: native colour-depth/format queries and writes use `NvAPI_Disp_ColorControl`, preserving other colour fields and checking the requested combination. Active-display lookup uses `NvAPI_GPU_GetConnectedDisplayIds`. The first active display on the selected GPU is used; there is no multi-monitor selector yet.
- NVIDIA: G-SYNC global mode uses documented `VRR_MODE_ID` choices. FXAA uses `FXAA_ENABLE_ID`. Existing DRS mappings for anisotropy, shader cache and power mode now match their UI value types/keys. Choice lists omit values without implemented mappings.
- NVIDIA: fan RPM now uses the public tachometer query. Unavailable sensor values remain unavailable. Driver installation can replace a simulated provider with a live provider.

This is not complete NVAPI coverage. DSR, integer/GPU scaling, image scaling/sharpness, texture-filtering quality and HDCP detection still have unavailable native paths. Some AMD 3D settings also remain unavailable. Unsupported controls must not be described as working hardware controls. NVIDIA automatic overclocking and performance targets remain in NVIDIA app; there is no verified public NVAPI setter in the checked SDK for this app-specific tuning workflow. The custom fan-curve editor and manual tuning wizard are removed from the NVIDIA UI. No standalone zero-RPM switch was added.

## Official references checked

- [NVIDIA app features](https://www.nvidia.com/en-us/geforce/news/nvidia-app-download-and-features/): automatic tuning and voltage, power, temperature and fan-speed targets.
- [NVAPI public interface inventory](https://github.com/NVIDIA/nvapi/blob/main/nvapi_interface.h)
- [NVAPI structures and display API](https://github.com/NVIDIA/nvapi/blob/main/nvapi.h)
- [NVIDIA driver-setting constants](https://github.com/NVIDIA/nvapi/blob/main/NvApiDriverSettings.h)
- [AMD display settings](https://github.com/GPUOpen-LibrariesAndSDKs/ADLX/blob/main/SDK/Include/IDisplaySettings.h)

## Verification

`dotnet run --project RionSuite/tests/GpuWorkflow/GpuWorkflow.csproj`

Checks distinguish app windows from installers, reject unhealthy/unknown/wrong-vendor/basic drivers, verify native display structure sizes, check wizard gating and package re-entry, and render the themed monitor, tweaks, display and tuning screens. These are software regression checks, not live-driver write validation. Actual installation, monitor blackout rollback, reboot-required drivers, and vendor hardware tuning still need testing on AMD/NVIDIA systems.

## Useful next integrations

1. [Intel PresentMon](https://github.com/GameTechDev/PresentMon) for frame-time captures and before/after comparison of each tweak.
2. [OCCT](https://www.ocbase.com/support/stability-certificate-gpu) as an optional externally launched GPU/VRAM stability check, with no automatic stress testing.
3. A per-machine change report containing the original setting, requested setting, driver readback, and one-click undo.

