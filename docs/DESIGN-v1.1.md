# Mouse-button HDMI switching for Samsung LU28R55

Implementation design · 12 November 2026 · Version 1.1

## 1. Goal, scope, and decision

Use an extra button on André's Logitech LIFT to alternate the Samsung monitor between the desktop PC and laptop. Keep both HDMI cables in place. Use the desktop's native Windows DDC/CI interface; do not use the monitor's USB service port or a video switch box.

Recommended MVP: Options+ launches a short-lived, silent `MonitorSwitch.exe` on each deliberate press. No arguments means toggle: read actual input, switch decimal 5 ↔ 6, verify, and exit. Do not synthesize shortcuts, intercept mouse events, or change Deskflow. Normal switching leaves the selected input in place; the five-second restoration used in earlier diagnostics is not product behavior.

This document is a specification, not evidence of installation or successful acceptance. The scaffold must fail safely until the Windows backend, enrollment, state machine, and acceptance gates are complete. No mouse binding, display setting, or Deskflow configuration is authorized by scaffolding alone.

### Two mandatory feasibility gates

1. **Fresh-process inactive-source control.** Select the laptop, terminate the switching process and release every handle. A newly launched process must rediscover the enrolled monitor, open fresh physical handles, read its actual input, and restore the PC. An earlier restore using a retained handle does not prove this architecture works. Record visual confirmation as well as API results.
2. **Physical trigger across Deskflow focus states.** Bind an innocuous no-argument probe first. Prove exactly one execution per press while focus is on either machine. A direct Options+ action that works only with PC focus does not meet the one-press goal.

If either gate fails, retain working components, mark the failed acceptance item explicitly, and follow §10. Never report a completed mouse-button solution from API tests alone.

## 2. Prior environment evidence and revalidation

The following was reported as observed on Windows computer `andrepaim` on 11 November 2026. This project has not independently reproduced it.

| Item | Prior finding | Qualification |
| --- | --- | --- |
| Monitor | Samsung LU28R55, manufacturer SAM, display ID SAM1017, manufacture year 2024 | Retail suffix and serial not established |
| Connections | Two computers connected to the two HDMI inputs | Owner report |
| Input devices | Logitech LIFT and ERGO K860 | Windows inventory |
| Options+ | 1.93.755983 running | Recheck before setup |
| Deskflow | 1.27.0; screens andrepaim and M92GD4KJXX | Confirm server/client roles, physical mouse connection, laptop OS |
| Deskflow configuration | No configured hotkeys; enter/exit command hooks disabled | Preserve this configuration |
| Input feature | VCP byte 0x60, decimal 96 | Not decimal 60 |
| Device input values | Decimal 5 and 6, hex 0x05 and 0x06 | Physically tested device profile, not standard HDMI constants |
| Prior native cycle | Read 5, write 6, read 6, restore 5, read 5; repeated, with one five-second alternate-input interval | Whether handles were reacquired between processes is unestablished |
| Inactive-source control | PC successfully restored while alternate input selected | Current cable/GPU configuration only; fresh-process gate still required |
| Visual evidence | Owner said “yes worked” | Physical HDMI1/HDMI2 and PC/laptop label calibration still needed |

Capabilities advertised `60(01 03)` and the reply maximum was 3. Input Select is non-continuous, so the API's maximum output is undefined. Do not reject tested values 5/6 for exceeding 3 or substitute standard HDMI values 17/18. Capability strings remain advisory.

Recheck Windows version, architecture, GPU/cable route, device identity, installed SDK, Options+/Deskflow versions, mouse pairing, and app-specific profiles before Windows setup. Preserve evidence separately from assumptions.

## 3. User experience and constraints

- One deliberate press changes video once; a later press changes it back, including with the laptop image displayed.
- No terminal flash, success dialog, foreground activation, cloud service, telemetry, network endpoint, new account, admin requirement, kernel driver, external monitor utility, service, updater, tray app, or mouse hook.
- Write only VCP 0x60, only to the explicitly enrolled physical monitor, only values 5/6. Do not change brightness, power, resolution, refresh rate, GPU settings, or unrelated monitors.
- Read actual input every time; do not maintain alternating parity. Unknown input fails closed.
- The PC must be awake, powered, and signed in; Options+ must run; the LIFT must remain physically connected to Windows. Bluetooth Easy-Switch to the laptop is different from Deskflow focus and prevents this Windows launcher from receiving the button.
- Video switching does not move keyboard/mouse focus. Preserve the owner's existing Deskflow focus method. No automatic edge/focus hooks.
- A sleeping/non-transmitting laptop may produce a blank image. DDC readback is not proof of valid video. GPU topology may change after switching.

Prefer the side button currently assigned Forward, preserving Back and primary buttons. Inspect the actual LIFT diagram and existing assignment; show them and confirm the owner's choice before replacing it. Preserve a screenshot/plain-text record or supported backup. Never bind left/right click, scrolling, power, or underside Easy-Switch.

Options+ must emit one launch per deliberate press and no repeated launches while held; validate this experimentally. The helper suppresses overlapping and closely spaced launches. A finite cooldown cannot prevent oscillation from indefinitely repeated triggers beyond the cooldown. If held-button repeats cannot be disabled through supported UI, the existing architecture fails that acceptance item and requires a revised design.

## 4. Architecture and implementation stack

```text
LIFT side button → Options+ All Applications action → absolute MonitorSwitch.exe
  → validate command/config → exclusive operation lock
  → pending reconciliation or cooldown
  → uniquely locate enrolled physical monitor → read actual input
  → persist pending operation → exactly one SetVCPFeature
  → bounded readback → atomically complete authoritative state
  → derived local reports → dispose handles and exit
```

Deskflow remains unchanged. Use C#/.NET 10 LTS, `net10.0-windows` for Windows projects, and a self-contained Windows `WinExe`. Verify architecture before publishing `win-x64`; provide another build when needed. NativeAOT is optional. Use reviewed/generated P/Invoke to user32.dll and dxva2.dll; do not compile PowerShell dynamically on each click. Pure contracts and tests may target `net10.0` for deterministic non-Windows testing.

```text
src/MonitorSwitch/                 WinExe, command boundary and reporting
src/MonitorSwitch.Core/            Pure models, validation, orchestration contracts
src/MonitorSwitch.Windows/         Native discovery, DDC and handle ownership
src/MonitorSwitch.TriggerProbe/    Dedicated no-argument WinExe, no DDC dependency
tests/MonitorSwitch.Tests/         Unit and fake-backend tests, no hardware writes
docs/SETUP.md                     Enrollment, calibration, binding and recovery
docs/ACCEPTANCE.md                Actual evidence and pending acceptance items
docs/ROLLBACK.md                  Exact removal and assignment restoration
```

Scaffolding is a distinct deliverable: project references, safe entry points, contracts, sample configuration, tests, scripts and documentation. Unsupported behavior must return an explicit failure; never simulate successful native discovery or switching.

## 5. Commands, enrollment, and configuration

| Invocation | Contract |
| --- | --- |
| No arguments / `toggle` | Toggle only with valid enrollment |
| `discover --json --output PATH` | Read-only inventory; does not enroll or bind |
| `status --output PATH` | Read configured target/current input; preserve pending state |
| `diagnose --output PATH` | Read-only support report; capabilities advisory |
| `probe-trigger` | Append timestamp, PID, version and probe outcome; no config, lock, state, or DDC dependency |
| `set --input 5` / `set --input 6` | Idempotent explicit target; reject everything else |
| `toggle --dry-run` | Read-only plan; never clear pending or write monitor/state |
| `enroll --device-path PATH --edid-hash HASH` | Explicit selection from fresh discovery; no DDC writes or button changes |

An explicit output path is only a report destination, never an alternative config source. WinExe output may be invisible to a terminal; use structured files and exit codes. PowerShell examples use `Start-Process -Wait -PassThru` for exit-code collection. No arbitrary VCP or shell-command configuration.

Per-user files under `%LOCALAPPDATA%\MonitorSwitch`:

```text
bin\MonitorSwitch.exe
bin\MonitorSwitch.TriggerProbe.exe
config.json
state.json
last-result.json
logs\YYYY-MM-DD[.sequence].jsonl
logs\trigger-probe.jsonl
```

```json
{
  "schemaVersion": 1,
  "monitor": {
    "manufacturer": "SAM",
    "productId": "SAM1017",
    "friendlyName": "LU28R55",
    "deviceInterfacePath": "<captured during enrollment>",
    "edidIdentityHash": "<sha256 of raw validated EDID bytes>",
    "requireUniqueMatch": true
  },
  "input": {
    "vcpCode": 96,
    "allowedValues": [5, 6],
    "labels": {"5": "Input 5", "6": "Input 6"}
  },
  "operation": {
    "cooldownMs": 1500,
    "readAttempts": 3,
    "verificationDeadlineMs": 8000
  },
  "feedback": {"successSound": false, "errorSound": true}
}
```

Reject unsupported schema, unknown/duplicate fields, missing/placeholder identity, wrong VCP, non-unique targeting, and any allowed-value set other than exactly 5/6. Bound cooldown to 1500–10000 ms, read attempts to 1–3, and verification deadline to 1000–8000 ms. Labels are cosmetic; do not infer HDMI1/2 or PC/laptop names from capabilities.

Enrollment re-enumerates under the operation lock, requires an exact unique device-path/hash selection and the demonstrated SAM/SAM1017 profile, and displays the selected evidence. Hash the complete raw EDID (base block plus advertised extension blocks) with SHA-256, lowercase hex; require valid block lengths/checksums and complete extensions. Store the algorithm identifier in enrollment implementation metadata if a future format changes. Do not normalize away identity bytes. If identity cannot be obtained unambiguously, refuse enrollment.

Do not silently overwrite enrollment. Re-enrollment requires an explicit replacement option and selected fresh discovery evidence; it refuses while a pending operation exists. A changed path with matching EDID can be proposed by read-only diagnostics, never silently accepted by normal toggle. Back up prior valid config. Do not write serials or full paths in ordinary logs.

## 6. Native discovery and handle ownership

Join active Windows display paths to the GDI display name and HMONITOR using EnumDisplayMonitors, GetMonitorInfo, display configuration/device info, and SetupAPI EDID as needed. WMI evidence may supplement discovery but its array order is never a mapping. Do not persist “first”, primary monitor, cursor position, or enumeration index.

Use GetNumberOfPhysicalMonitorsFromHMONITOR followed by GetPhysicalMonitorsFromHMONITOR. An HMONITOR may have multiple physical monitors; refuse writes if the physical association is ambiguous. Require exact enrolled interface path and EDID hash, including after one permitted re-enumeration. Active-path disappearance must fail closed, not redirect to another display.

Use GetVCPFeatureAndVCPFeatureReply(handle, 0x60, ...) to read and SetVCPFeature(handle, 0x60, target) for one write. Revalidate 5/6 inside the Windows backend. Dispose all acquired physical handles exactly once on every path, using either an owned array or individual SafeHandles without double ownership. Never keep a native handle between process invocations.

Interop uses pointer-width-safe handles, Windows BOOL marshaling, Unicode fixed-length physical description buffers, and SetLastError where applicable. Capture the error immediately after failure. Native API acceptance does not prove input selection or visible content. Test the mapping on this machine; monitor implementations vary.

## 7. State machine, persistence, and results

All commands using native monitor access coordinate through the same nonblocking exclusive lock. Busy requests never queue. Read-only commands may return Busy; they must not reconcile/clear pending state, change cooldown, or overwrite the active writer's authoritative state. Reports use unique temporary files; requested outputs must not alias config/state/log files.

The lock must cover every process that can access the same per-user state. A `Local\MonitorSwitch-<user-id-hash>` mutex is session scoped: enforce one participating interactive session or use a reviewed cross-session lock. An abandoned lock requires reading durable state before any new write. Named mutex ownership is thread-affine; do not release from an unrelated continuation thread.

`state.json` is the single authoritative atomic document: schema, enrolled identity (device path and EDID hash, matching config; a mismatch fails closed), pending operation, last completed outcome, and boot-aware monotonic cooldown metadata. Logs and last-result are derived reports, not recovery authority. Missing state at first use is allowed; corrupt existing state fails closed. A reboot/clock correction must not create infinite cooldown. Pending state survives reboot and app restart.

1. Validate command/config. Acquire lock without waiting.
2. Load authoritative state. If pending exists, perform read-only reconciliation and return without switching. Target or original readback resolves it; unreadable or unexpected values retain pending and fail closed.
3. Apply cooldown after the last completed attempt. Busy/debounced presses do not extend it indefinitely.
4. Freshly locate exactly one target. Read up to three times (for example at 0, 200 and 500 ms). Unreadable or non-5/6 input causes zero writes.
5. Compute target. Explicit set to current target returns AlreadySelected without a write.
6. Persist pending operation atomically, including ID, identity fingerprint, command, before and target, before entering Set. Failure prevents Set.
7. Call Set exactly once, regardless of API return. A failed return can still mean the monitor changed.
8. Poll with bounded backoff until target readback or the verification deadline. Permit one re-enumeration for invalid handles, requiring the exact same identity. Never retry Set or blindly roll back.
9. Confirmed means target observed, including when Set reported failure (record both). NotChanged requires at least two successful original-value samples, one completed during the final 1000 ms of the verification window, with no unexpected-value sample. Mixed/inadequate reads, unexpected values, or inability to obtain a final sufficiently recent sample mean OutcomeUnverified.
10. Atomically replace authoritative state with completion and cooldown. Retain pending for unresolved outcomes. If completion persistence fails, leave the prior pending record intact and report the persistence failure. Only then emit derived reports and release handles/lock.

The deadline bounds retry scheduling, not the duration of blocking native calls. Every result is an observation at a time, not proof of future monitor behavior. An async timeout cannot cancel native DDC. A blocked one-shot process must retain exclusivity; document manual termination and next-launch reconciliation. If a hard deadline is later required, separately design supervised child-process DDC with a Windows Job Object. Never abandon a live writer while permitting another.

Durability contract: write a same-directory temporary file, flush file data, then atomically replace/rename with platform-appropriate semantics. Do not describe multiple separate file replacements as one transaction. Test interruptions around pending save, native write, completion save, and reporting. An interrupted pre-write pending record is reconciled conservatively too. Reporting failure never clears pending. A successful durable completion may be reported with a reporting warning if a derived output fails.

| Exit | Outcome |
| --- | --- |
| 0 | Confirmed / AlreadySelected; successful read-only/probe/enrollment commands |
| 2 | Busy / Debounced |
| 10 | InvalidConfiguration or invalid command |
| 11 | TargetMissingOrAmbiguous |
| 12 | ReadUnavailable |
| 13 | UnexpectedInput |
| 14 | NotChanged |
| 15 | OutcomeUnverified, including unresolved reconciliation |
| 16 | ReconciledWithoutToggle |
| 20 | InternalError / persistence failure |
| 21 | NotImplemented (scaffold only; never success) |

Local JSONL includes timestamp, version, operation ID, identity hash, command, before/target/observed, native return/error, elapsed time, and outcome. No credentials, window titles, hardware serials, or telemetry. Rotate at 1 MB per file and retain at most seven days, including probe logs. Optional short error sound must not steal focus. Busy/read-only reports must not race an active operation's last-result update.

## 8. Options+ and Deskflow validation

Build a dedicated no-argument probe executable that only appends timestamp/PID/version/outcome and exits. It must not require enrollment, open DDC, or share switching state. Safely serialize concurrent probe appends without losing evidence; a logging failure returns failure.

Temporarily assign the selected button through supported Options+ UI. Prefer ordinary Open Application/Open File when available; otherwise Smart Action Device trigger → Application Open with an absolute executable path. Inspect actual UI labels/version. Smart Actions require an Options+ account; do not create/sign into one silently. Inspect app overrides. Never edit private Options+ databases or fabricate import schemas. Use supported export where available and record the old assignment.

Test ten separated presses in each relevant state:

- PC image, PC Deskflow focus.
- Laptop image, PC focus.
- Laptop image, laptop focus.
- PC image, laptop focus.
- Deskflow disconnected/stopped while preserving the owner's normal session.

Require exactly ten executions, no duplicate or forwarded Back/Forward behavior, no focus change, and no launch-once/foreground-existing behavior. Test a long hold. Then repeat lifecycle checks with the real helper running verification and with an intentionally blocked fake backend: launches must not queue or foreground an existing process, and later deliberate presses must work after exit. Preserve the distinction between an action launch and an actual accepted switch.

Deskflow keyboard hotkeys and mouse actions have different behavior; documentation states mouse-button actions are not handled while the cursor is on the server. Inspect installed 1.27 help before any separately approved integration. MVP adds no bindings, enter/exit commands, or focus synchronization.

## 9. Implementation, packaging, and rollback

1. Inspect repository instructions and host/toolchain without changing devices. Create this standalone project; preserve other repositories.
2. Scaffold safe contracts/probe and documentation. Build pure state machine and fake backend; finish meaningful tests before hardware writes.
3. Implement read-only discovery/status/enrollment, validate identity and input values on Windows.
4. Tell the owner before supervised physical switching and explain physical-button recovery. Test explicit 5→6→5, visual behavior, and the fresh-process inactive-source gate.
5. Run probe-trigger matrix and hold tests. Only after it passes replace the probe assignment with the real executable and run real-process lifecycle checks.
6. Complete hardware acceptance and record evidence. No repository publication or service installation required.

Publish Release self-contained for verified architecture, retain symbols separately, and hash artifacts with SHA-256. WinExe must not flash a console. Review single-file/native extraction behavior; a normal self-contained folder is acceptable. No runtime download on first press, execution-policy change, AV exclusion, global PATH edit, scheduled task, elevation, or startup registration. Do not claim signing without a real signature. Self-contained runtime servicing requires rebuilding/replacing the package with patched runtime versions.

Install to the stable per-user bin only after setup approval. Copy the whole tested package, not just an exe if dependencies are separate. Update only with no operation active; keep one previous package/config backup and preserve enrollment/button selection.

Rollback: restore only the prior selected-button assignment, remove only the dedicated Smart Action if created, confirm no helper instance runs, then remove only this app's files. Offer to preserve diagnostic logs. Do not reset all mouse settings or Deskflow. Restore a recorded input explicitly when its state is understood, or use the physical monitor control. Removal must not change HDMI/GPU/display settings.

## 10. Fallback decisions

- Both feasibility gates pass: proceed with the one-shot design.
- Direct launch only works with PC focus: inspect supported action/profile choices and physical mouse connection. If still failing, report a blocker.
- Owner accepts two steps: a documented Deskflow hotkey returns focus to PC, then the mouse button; label this a workaround, not the requested outcome.
- Owner requires one press despite trigger failure: separately scope Deskflow integration or a dedicated HID/serial button. No low-level hooks, drivers, network listeners, or focus hooks without revised design and approval.
- Fresh-process discovery/DDC fails on alternate input: report that architectural blocker. Do not silently introduce a resident handle keeper or alternate targeting.
- Topology/driver changes break discovery/readback: fail closed, diagnose cable route/identity, and explicitly re-enroll only when appropriate. Do not guess new codes, wake the laptop remotely, or reboot devices.
- PC asleep/off or mouse paired elsewhere: use the physical monitor control. Independent switching hardware is out of scope.

## 11. Tests and acceptance

Automated tests use deterministic fakes and never touch hardware:

- 5→6 and 6→5; no arguments equals toggle; selected explicit set causes zero writes.
- Bad JSON/schema/unknown fields/identity, wrong VCP/values, missing or duplicate target, and unexpected current input cause zero writes.
- Maximum=3/capabilities do not reject the demonstrated 5/6 profile.
- Set accepted/failed with target readback; old-value verification; mixed/unexpected/unreadable samples; exact outcome rules and at most one Set under all failures.
- Concurrent commands, thread-safe lock release, no queued switches, cooldown/boot/clock behavior, corrupt existing state.
- Pending reconciliation never writes, including unexpected current input; read-only commands never resolve pending.
- Interruption at every persistence boundary; reporting failure cannot lose pending; completion is one authoritative replacement.
- Same-identity re-enumeration only; topology cannot redirect a write; every native handle disposed exactly once.
- Paths with spaces/non-ASCII work; no shell interpretation; report paths cannot overwrite protected app files.
- Probe independent of config/DDC, concurrent append evidence, bounded retention, explicit errors.

Hardware acceptance records date, binary hash, relevant identity/GPU/cables, architecture, Options+/Deskflow versions and actual outcomes without unrelated private data. Required checks:

| Test | Required result |
| --- | --- |
| Manual explicit PC→laptop→PC | Correct readbacks and owner-confirmed images |
| Fresh process on laptop input | Reacquires correct identity/handles and restores PC |
| Probe matrix | Ten executions for ten separated presses per relevant state |
| Real helper busy/verification lifecycle | No foreground activation or queue; later presses work |
| Desktop/browser/editor overrides | Correct behavior or explicitly excluded override |
| Ten complete round trips | Correct images/readback, no orphan processes or unrelated writes |
| Rapid presses and long hold | Overlap/debounce safe; no held-button repeats or delayed oscillation |
| Physical OSD change | Next switch reads actual input |
| Disconnect/reconnect | No wrong-target write; exact identity required |
| PC sleep/resume | No asleep execution; works after session/Options+ resume |
| Laptop asleep | No valid-video claim; documented recovery works |
| App missing / Options+ stopped | No harmful fallback; clear diagnosis |
| Rollback | Prior assignment restored; Deskflow unchanged |

Measure, do not promise, warm launch-to-write under one second and ordinary verification within eight seconds. Signal lock may take longer; native hangs remain possible.

Definition of done for the full implementation: source/build instructions delivered, tests pass, native switching and fresh-process gate pass on this monitor, trigger matrix passes including laptop-focused Deskflow, owner sees correct images, prior assignment and rollback recorded, unrelated settings unchanged. A scaffold is not this full acceptance. A failed gate remains an explicit blocked acceptance item.

## 12. Internal contracts and build commands

Keep a clock/delay abstraction, locator, operation lock, atomic state store and reporter injectable. Models include MonitorIdentity, InputRead, WriteReply, SwitchResult and PendingOperation. IMonitorSession owns all physical handles, reads only 0x60, and validates 5/6 again before writes. IMonitorLocator opens an exact unique identity. IOperationStore loads/replaces the single authoritative state document; reporting is separate. Catch boundary exceptions and return structured failures without a stack-trace dialog.

Expected PowerShell build pipeline (from repo root):

```powershell
dotnet restore MonitorSwitch.slnx
dotnet build MonitorSwitch.slnx -c Release --no-restore
dotnet test MonitorSwitch.slnx -c Release --no-build
dotnet publish .\src\MonitorSwitch\MonitorSwitch.csproj `
  -c Release -r win-x64 --self-contained true -o .\artifacts\win-x64\app
dotnet publish .\src\MonitorSwitch.TriggerProbe\MonitorSwitch.TriggerProbe.csproj `
  -c Release -r win-x64 --self-contained true -o .\artifacts\win-x64\probe
# Or: .\scripts\Publish.ps1 -Runtime win-x64 (also writes per-folder SHA-256 manifests)
```

Each output is a self-contained folder; hash and deploy the whole folder, not only the apphost. Single-file packaging is optional future work (§9). Publish the probe separately to avoid output collisions. Run published read-only diagnostics on Windows before binding. Hardware tests are explicit manual integration checks, never ordinary CI. Scaffolding scripts must not install, enroll, bind, or switch inputs.

## 13. Instructions for implementation

Implement this MVP rather than a larger platform. Start with read-only native discovery and the harmless probe. Recheck actual connected devices and software. Do not use monitor USB, arbitrary VCP access, generic HDMI substitutions, drivers, security changes, publication, or network services. Preserve other repositories/settings. Before binding, show the exact button and assignment. Before physical tests, explain recovery and obtain the owner's visual confirmation. Keep video and Deskflow focus separate unless scope explicitly expands. Final implementation handoff includes executable path, SHA-256, evidence, limitations and exact rollback.

## 14. Official references and v1.1 changes

Official documentation checked during design review on 12 November 2026; device-specific evidence remains the reported local 11 November tests.

- [Microsoft GetVCPFeatureAndVCPFeatureReply](https://learn.microsoft.com/en-us/windows/win32/api/lowlevelmonitorconfigurationapi/nf-lowlevelmonitorconfigurationapi-getvcpfeatureandvcpfeaturereply)
- [Microsoft SetVCPFeature](https://learn.microsoft.com/en-us/windows/win32/api/lowlevelmonitorconfigurationapi/nf-lowlevelmonitorconfigurationapi-setvcpfeature)
- [Microsoft physical-monitor enumeration and cleanup](https://learn.microsoft.com/en-us/windows/win32/monitor/using-the-low-level-monitor-configuration-functions)
- [Microsoft DestroyPhysicalMonitors](https://learn.microsoft.com/en-us/windows/win32/api/physicalmonitorenumerationapi/nf-physicalmonitorenumerationapi-destroyphysicalmonitors)
- [Logitech Options+ Smart Actions](https://hub.sync.logitech.com/options/post/using-smart-actions-in-options-FAU1yARC27jYpg0)
- [Deskflow configuration](https://github.com/deskflow/deskflow/blob/master/docs/Configuration.md)
- [Microsoft .NET 10 overview](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)

Changes from v1.0: added fresh-process inactive-source gate; corrected hold/debounce guarantee; defined one authoritative state transaction and failure handling; made verification/reconciliation cases explicit; specified enrollment/hash contract; coordinated read-only/native operations; added real-helper launcher lifecycle checks; distinguished scaffold completion from full hardware acceptance.
