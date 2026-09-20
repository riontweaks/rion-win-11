# Rion experiment manager — private preview

Research checked September 6, 2026. This is an engineering guide, not evidence that a particular tweak improves performance.

## What is implemented

Open Tweaks → Experiment manager in **Rion Experiment Preview.exe**. The other app features remain available. The preview feature is compiled only when `RionExperimental=true`; ordinary builds exclude its code and view.

The first two experiments set the active plan's AC minimum processor state to 5% or 100%. These are bounded comparison targets, not recommendations or guaranteed CPU frequencies. If the plan already uses the target, capture is rejected. Windows and firmware decide actual operating behavior.

1. Describe the workload, measurement tool, scene and settings. Select a metric expressed in milliseconds.
2. Capture the original setting. This reads the current plan and records the Windows build and CPU information; it does not apply a change.
3. Run the same benchmark at least three times. Enter a single comparable measurement from each run. Use decimal points, one number per line. Do not paste FPS into a milliseconds field.
4. Save baseline runs, then apply the experiment. The journal is committed before the write, and the requested setting is read back.
5. Repeat the workload under the same conditions and save trial results.
6. Restore the original setting. Run the workload again and save restored-phase measurements to check that results reproduce.
7. Export a Markdown report. It contains the measurements, environment, exact before/target values, state and comparison.

Mean, range and percentage changes describe the entered numbers. They are not a statistical significance test. Three runs are an input minimum, not sufficient evidence by themselves. Actual input-to-display latency needs suitable instrumentation; frame times do not substitute for it.

## Recovery and boundaries

Journals live in `%LOCALAPPDATA%\RionWin11\Experiments`. One open experiment is allowed across processes sharing this location. The manager checks the captured plan and AC power before applying or accepting results. Do not use the other tweak pages during a comparison: only the experiment's own setting is tracked, not all possible system changes.

If a write or refresh fails, the applying/restoring journal remains available. Reopen the page and choose Restore original. A third-party value different from both original and target stops restoration instead of being overwritten. If interrupted while capturing, no setting was written. Cancel closes an unapplied session without changing the machine.

There is no background recovery service, automatic boot action, crash-proof guarantee or full system image backup. A successful read-back verifies the stored power-policy value, not hardware response or improved latency. Firmware, drivers and unsupported hardware can still affect behavior. Keep a recoverable system backup before broader experiments.

## How the Windows kernel participates in latency

An application's work passes through several layers. Its threads need CPU time; memory references may require page handling; file/network operations reach drivers; graphics work queues for GPU execution and display. Kernel subsystems coordinate these paths. The slowest dependency can dominate responsiveness even when total CPU utilization looks low.

### Scheduling and synchronization

Windows selects ready threads using priorities, with priority classes and boosts influencing execution. Raising one thread's priority can delay other work on which it depends. Real-time priority can interfere with input and disk-flushing system threads. First investigate ready-thread delays, contention and waits instead of assigning every game a high priority. See [Scheduling priorities](https://learn.microsoft.com/en-us/windows/win32/procthread/scheduling-priorities) and [CPU analysis](https://learn.microsoft.com/en-us/windows-hardware/test/wpt/cpu-analysis).

**Research candidate:** temporary, per-process scheduling changes only if traces show a relevant CPU scheduling bottleneck. This is not implemented in the preview. Any future experiment must restore the original process state and account for process exit or PID reuse.

### Driver interrupts and deferred work

Devices interrupt the CPU. Interrupt service routines handle immediate work; deferred procedure calls handle additional work at an elevated execution level. Long routines can delay ordinary threads, but a large isolated DPC number does not prove it caused a visible stutter. Correlate the driver activity with the problematic event in a trace. See [DPC/ISR measurement](https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/example-15--measuring-dpc-isr-time) and [CPU analysis](https://learn.microsoft.com/en-us/windows-hardware/test/wpt/cpu-analysis).

**Research candidate:** compare an identified device's vendor-supported driver versions or settings. Do not bundle interrupt affinity, MSI mode and offload changes into a universal profile. Interrupt tracing is future work, not an existing manager feature.

### Processor power management

Windows requests performance levels while platform firmware and hardware enforce power and thermal constraints. A high minimum setting can trade energy for a different response under some workloads; it may do nothing useful or worsen sustained behavior through heat. The preview isolates this policy variable so it can be measured. Microsoft documents minimum/maximum processor state as percentages in its [power and performance tuning guide](https://download.microsoft.com/download/9/b/2/9b205446-37ee-4bb1-9a50-e872565692f1/perftuningguideserver2012r2.pdf). That server-era documentation explains the parameter; it is not evidence of a gaming benefit on this PC.

**Research candidate:** record effective clocks, package power and temperature alongside frame-time results. Automatic sensor capture is not included yet. Record relevant values in the workload description or external benchmark notes for now.

### Virtual memory and storage

A working set represents pageable memory resident in RAM. A soft page fault can be resolved from memory; a hard fault requires backing-store access. Reducing a displayed memory count by clearing working sets can create additional faults later. See [Working sets](https://learn.microsoft.com/en-us/windows/win32/memory/working-set).

**Research candidate:** correlate hard faults and storage waits with stutters, then compare the actual application working set and competing processes. Disabling the page file, periodic RAM cleaning and arbitrary cache registry values are not included in this preview.

### Timers and boot flags

A timer request, a high-resolution clock measurement, a driver interrupt and end-to-end input latency are different concepts. Microsoft marks several BCD timer overrides as debugging options. Their existence does not establish them as performance recommendations. See [BCDEdit /set](https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/bcdedit--set).

**Research decision:** no new BCD/timer experiments yet. Audit the existing Dynamic Tick wording and preserve exact absent/present BCD state before considering it eligible for experiments. Existing app tweaks are not automatically validated by inclusion in this private build.

### Kernel extensions and a custom kernel

The Windows kernel is not replaced by changing registry values. A KMDF driver is a kernel-mode extension, not a replacement scheduler. Microsoft documents kernel patching as unsupported outside authorized Microsoft hot patches; protected code/data changes can cause bug check 0x109. See [Kernel protection](https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/bug-check-0x109---critical-structure-corruption).

Driver development would require a specific measured need, documented interfaces, isolated testing, debugging, signing and compatibility validation. Driver Verifier intentionally bug-checks on violations and belongs on a test installation. See [KMDF tutorial](https://learn.microsoft.com/en-us/windows-hardware/drivers/gettingstarted/writing-a-kmdf-driver-based-on-a-template), [Driver Verifier](https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/driver-verifier), and [signing options](https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/driver-signing-offerings). This preview installs no driver and changes no kernel security protections.

## Evidence needed before expanding the app

- State a specific hypothesis and metric before testing. Identify an effect large enough to matter and larger than measurement noise.
- Keep workload, resolution, frame cap, drivers, power source and background activity consistent. Warm up consistently and preserve raw results.
- Repeat baseline/change/restored-baseline runs. Alternate test order in later sessions to expose temperature and cache effects.
- Investigate regressions in other workloads, networking, audio and sleep/wake. Re-test after a Windows, firmware or driver update.
- Classify outcomes as promising, inconclusive or regressed. Do not promote one user's result to a universal recommended value.

Next implementation priorities: import benchmark measurements with explicit units, capture environment changes between phases, add optional WPR trace collection after inspecting existing recording sessions, and correlate measurements with driver/CPU activity. Kernel code comes only after tracing demonstrates a concrete need unsupported by ordinary APIs.

## Building this preview

`dotnet publish RionSuite/src/RionHub/RionHub.csproj --no-restore -c Release -p:Platform=x64 -p:RionExperimental=true -o RionSuite/publish/experiment-preview`

The internal assembly name remains unchanged for WPF resource compatibility. Copy the resulting EXE as `Rion Experiment Preview.exe`; do not overwrite the official Desktop executable. Public distribution has not been performed.
