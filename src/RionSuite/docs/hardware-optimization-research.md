# Memory, NVMe and input optimization review

Reviewed 2026-09-06. The scan now reads these categories and reports the actual configuration. No new blanket performance overrides are applied.

| Area | Evidence and decision |
| --- | --- |
| Memory | Read available memory, commit charge/limit and automatic page-file management. Page-file sizing depends on peak commit and crash-dump requirements, not one fixed RAM multiplier. A single snapshot is not enough to diagnose sustained pressure. Do not automatically disable the page file or introduce a recurring RAM cleaner. |
| NVMe | Detect devices exposed as NVMe by the Windows storage provider and show their health status. StorNVMe idle timeouts and latency tolerances control a real power/latency tradeoff. This does not establish an across-the-board gaming benefit from disabling power management. Leave cache, queue-depth and power overrides out of automatic recommendations; measure the affected workload before changing them. |
| USB | Read the global suspend override, with a note that device and power-plan policy also apply. Microsoft strongly recommends leaving selective suspend enabled. The existing disable toggle is now explicitly a manual troubleshooting choice, not a universal latency improvement. |
| Mouse | Read pointer preferences. Changing acceleration can be a useful personal preference for desktop/legacy input. Raw-input motion bypasses Control Panel mouse speed. Do not advertise registry pointer changes as reduced hardware polling latency. |
| Keyboard | Read repeat speed/delay. These affect held-key repetition, not initial key latency or device report rate. Keep accessibility settings intact; do not add undocumented queue-size or filter-key performance presets. |

Potential next additions are workload-based memory pressure sampling, storage latency measurements, and hardware-specific firmware/update links. These need their own measurement and compatibility work before becoming automatic settings.

## Primary sources

- [Microsoft: page-file sizing and system commit](https://learn.microsoft.com/en-us/troubleshoot/windows-client/performance/how-to-determine-the-appropriate-page-file-size-for-64-bit-versions-of-windows)
- [Microsoft: StorNVMe power management and documented settings](https://learn.microsoft.com/en-us/windows-hardware/design/component-guidelines/power-management-for-storage-hardware-devices-nvme)
- [Microsoft: USB selective suspend](https://learn.microsoft.com/en-us/windows-hardware/drivers/usbcon/usb-selective-suspend)
- [Microsoft: RAWMOUSE and Control Panel pointer settings](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawmouse)
- [Microsoft: keyboard repeat-speed semantics](https://learn.microsoft.com/en-us/dotnet/api/system.windows.systemparameters.keyboardspeed)
- [Microsoft: Windows improvements for high report-rate mice](https://blogs.windows.com/windowsdeveloper/2023/05/26/delivering-delightful-performance-for-more-than-one-billion-users-worldwide/)
