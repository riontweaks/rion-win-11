# AMD cooling-curve recommendation

Research and implementation review: 2026-09-06. Scope: the selected AMD adapter in Better Adrenalin / Overclocking / Fan Tuning. No fan recommendation is automatically applied by graphics profiles, installation, or startup.

## Finding

There is no justified universal temperature/PWM table for every AMD GPU. The recommended option is an explicitly selected, modest cooling-oriented **starting point based on the current driver curve**. It is not necessarily based on factory settings. Factory automatic control remains a reasonable default; users seeking lower noise should not interpret this cooling preset as a quiet preset.

## Primary evidence and implications

| Source | Finding | Implementation consequence |
| --- | --- | --- |
| [AMD Adrenalin tuning guide](https://www.amd.com/en/resources/support-articles/faqs/DH3-020.html) | Controls vary with hardware. Advanced fan control uses temperature and PWM percentage. Zero RPM permits fans to stop under light load. AMD separates tuning from stress testing. | Capability checks, explicit apply, keep existing Zero RPM, no claim of tested stability. |
| [AMD legacy WattMan guide](https://www.amd.com/en/resources/support-articles/faqs/DH-020.html) | Older generations expose different tuning controls; increased fan speed can increase noise. | Do not infer support from the Radeon name or assume every device has five states. Disclose noise cost. |
| [ADLX GetFanTuningRanges](https://gpuopen.com/manuals/adlx/adlx-sdk-references/adlx-interfaces/gpu-tuning/iadlxmanualfantuning/getfantuningranges/) | Speed is percent, temperature is Celsius, and the returned ranges apply to the GPU's states. | Read actual ranges; reject unknown, nonfinite, invalid or off-grid values. |
| [ADLX IsValidFanTuningStates](https://gpuopen.com/manuals/adlx/adlx-sdk-references/adlx-interfaces/gpu-tuning/iadlxmanualfantuning/isvalidfantuningstates/) | A valid state list has error index -1, and execution has a result code. | Require successful validation and index -1 before submitting. |
| [ADLX SetFanTuningStates](https://gpuopen.com/manuals/adlx/adlx-sdk-references/adlx-interfaces/gpu-tuning/iadlxmanualfantuning/setfantuningstates/) and bundled SDK `IGPUManualFanTuning.h` | The call submits a list; the bundled header documents a reset-needed result when automatic tuning is enabled. | Require exact list length and successful individual setters. Surface failure; never reset all GPU tuning merely to make the fan preset work. |
| [PowerColor troubleshooting FAQ](https://powercolor.com/faq68.htm) | Hotspot is the hottest die sensor, distinct from core temperature. MuteFan stops fans below the board's threshold. Airflow and heatsink condition matter when investigating thermal problems. | Do not equate hotspot with the curve's temperature input or use a universal 100/110°C limit. Keep the current anchors and idle behavior. A curve is not a repair for cooling faults. |
| [SAPPHIRE cooling design](https://nation.sapphiretech.com/en/nitro-explore) and [fan accessory specifications](https://www.sapphiretech.com/en/accessories/nitro-gear-accessories) | Board cooling incorporates multiple component temperatures. Fan dimensions, RPM specifications and acoustic specifications vary across products. | PWM percentage is not a universal RPM or airflow measurement. No promised temperature reduction or noise level. |
| [ASUS fan start/stop conditions](https://www.asus.com/ca-en/support/faq/1044879/) | This NVIDIA-specific example documents model-dependent temperature and power conditions with distinct start/stop thresholds. | Corroborating evidence of board-specific policy only; those thresholds are **not** transplanted to AMD. |

The ADLX interface documentation does not establish one universal curve-control sensor identity across all boards. Preserving the driver's existing temperature anchors avoids inventing one. No forum temperature anecdotes were used as a thermal specification.

## Design hearing

**Advocate:** A small increase in existing nonzero PWM values offers a useful cooling-oriented starting point while preserving the driver's temperature anchors, state count and supported ranges. A pure policy function and fan-only recovery make behavior inspectable.

**Strongest objection:** A previously customized curve may already be poor, and supported values are not proof of adequate cooling. Higher PWM can be noisier without a useful temperature reduction. A single fixed table cannot account for fan hardware, cooler, ambient temperature, airflow or board firmware.

**Expected benefit:** More requested fan duty at the same existing anchors. Actual cooling, acoustics, FPS and stability are unmeasured.

**Cost:** Possible extra noise and fan power. The policy does not optimize for quiet operation. It cannot establish whether the existing curve is suitable.

**Rollback:** Flush the prior fan state to a local recovery journal before writing; verify the requested curve and unchanged Zero RPM by readback. On failure attempt fan-only restoration. Retain a failed recovery, including a readable partial state when possible, for a later retry. Refuse restoration after an unrelated fan-state change or a mismatched adapter/driver identity. No automatic startup writes and no factory reset of clocks, voltage or power.

**Verdict:** Ship the explicitly labeled starting preset and native validation fixes. Reject the claim that the numerical preset is universally optimal, vendor-certified, or safe on every GPU. Offer it only where capability and complete state reads succeed.

## Exact algorithm

For each existing point, keep its temperature. Preserve zero-duty points. Add `floor(5 / speedStep) * speedStep` percentage points to other speeds, capped at the highest supported grid value. This means an increase of **at most five percentage points**, not five percent of the current speed. The five-point budget is an app design choice, not a vendor recommendation. A step larger than five produces no increase. Preserve ordering; reject incomplete, decreasing, duplicate-temperature, nonfinite, out-of-range or off-grid source curves. No resampling to a fixed point count and no fabricated fallback curve.

Inspect is read-only. Apply compares the preview with current fan state and identity. Only one application is allowed until Restore, including after reopening the app, so repeated use cannot compound. Recovery files live under `%LOCALAPPDATA%\RionWin11\FanRecovery`; these are internal undo records, not a user profile library. Identity includes ADLX adapter ID, driver registry path, PCI identifiers, VBIOS, driver version and demo status. Driver updates or topology changes may require reviewing an old recovery file rather than automatically restoring it.

## Verification

Focused mock tests cover 24 combinations of state count and step size; preservation, validation rejection, stale previews, journal failure before writes, repeated application, reopening without replay, exact restoration, partial-write rollback, failed rollback with persisted retry, and external-change refusal. WPF rendering checks the themed fan card and bound controls. Solution build and release publish are recorded in `artifacts/amd-fan-*.log`.

Native ABI signatures and list semantics were checked against the bundled AMD headers. These tests do **not** validate native writes, actual fan RPM response, cooling, noise, thermals under load, cross-board compatibility or stability on hardware. No GPU settings were changed to test this feature.
