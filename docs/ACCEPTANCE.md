# Acceptance evidence

## Scope and provenance

The 11 November 2026 hardware findings in [design §2](DESIGN-v1.1.md#2-prior-environment-evidence-and-revalidation) were supplied in the proposal. They have not been reproduced by this repository. The current work runs on a Linux x86_64 development host; no Windows monitor, Options+, Deskflow or mouse settings were accessed.

## Scaffold checks — 12 November 2026

Actual local build/test evidence is recorded below after verification. These checks establish scaffold consistency only; they do not satisfy hardware acceptance.

| Check | Evidence |
| --- | --- |
| Design written first | `docs/DESIGN-v1.1.md` created before Git initialization and scaffold files |
| Git | Standalone repository, branch `main`; initially created without a remote |
| SDK | Temporary Linux SDK 10.0.404 under `/tmp/monitor-switch-dotnet-sdk` |
| Restore | `dotnet restore MonitorSwitch.slnx` succeeded for all six projects |
| Release build | `dotnet build MonitorSwitch.slnx -c Release --no-restore` passed; 0 warnings, 0 errors |
| Tests | `dotnet test MonitorSwitch.slnx -c Release --no-build` passed: 29 passed, 0 failed, 0 skipped |
| Windows packaging | Main app and probe cross-published with `-r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false` into separate ignored artifact folders |
| Executable format | Both apphosts inspected as PE32+ GUI executables, Windows x86-64 |
| PowerShell scripts / hosted CI | Provided; PowerShell scripts have not been executed here because PowerShell is unavailable on this host. Hosted CI results must be checked separately. |

Development package hashes (not installed or run on Windows):

| File | SHA-256 |
| --- | --- |
| `artifacts/win-x64/app/MonitorSwitch.exe` | `a66ce8ca6ecace56d67679081eac3dac1349837933877f45352d0f3db79e6e3b` |
| `artifacts/win-x64/probe/MonitorSwitch.TriggerProbe.exe` | `84a52e738ca5d2842717ac4d9a5c459bd604c86f02f5c38f2ffb296a4f3bf65f` |

SHA-256 manifests for every file in each folder are stored alongside the ignored packages. These are folder deployments; an apphost alone is not the complete package. The main package is intentionally nonfunctional for monitor commands. Native discovery, command report writing, full probe retention, and Windows runtime behavior remain pending.

## Implementation and read-only hardware checks — 12 November 2026

Run on the target Windows PC by the implementing agent. No monitor writes, button assignments or Deskflow changes were made.

| Check | Evidence |
| --- | --- |
| Host | Windows 11 Pro 10.0.26300, x64; NVIDIA GeForce RTX 3060; .NET SDK 10.0.404 |
| Build/tests | `scripts\Build.ps1`: 0 warnings, 0 errors; 129 passed, 0 failed |
| Package | `artifacts\win-x64\app\MonitorSwitch.exe` SHA-256 `390f862419cb3c103e4fef9b0e8852f0bea440d0a92a3d90c41d89a613beedd9` (development, unsigned) |
| Probe package | `artifacts\win-x64\probe\MonitorSwitch.TriggerProbe.exe` SHA-256 `11f51f1b2d27ffa90ad6ef645c099e6f0c40c4d1dc3f0bf8a680efc9a77c472b` |
| `discover` | Exit 0. One active target: SAM / SAM1017 "LU28R55" on `\\.\DISPLAY1`; EDID valid; 1 physical monitor; 1 target on source; VCP 0x60 read 5 |
| `enroll` | Exit 0. Enrolled the discovered path and EDID hash `e950ed17…a1c42` (full values in local config only) |
| `status` | Exit 0. Current input 5 ("Input 5"); no state, no pending |
| `diagnose` | Exit 0 in ~2.0 s. Capabilities (advisory) include `60(01 03)`, `mccs_ver(2.0)`, matching the prior report |
| `toggle --dry-run` | Exit 0 in ~0.44 s launch-to-exit (109 ms internal). Planned 5 → 6; no write; no `state.json` created |

## Supervised native switch and fresh-process gate — 12 November 2026

Same host and package hash as above. Owner present and watching the monitor; physical input button available for recovery.

| Step | Native evidence | Owner-confirmed image |
| --- | --- | --- |
| `set --input 6` (process A) | Exit 0, Confirmed: read 5, one `SetVCPFeature` accepted, readback 6; 1062 ms internal, 1.3 s wall | Laptop image |
| Process A exited | Zero MonitorSwitch processes alive; all physical handles released | Laptop image held ~10 s |
| `set --input 5` (new process B, laptop input selected) | Exit 0, Confirmed: fresh discovery found the enrolled identity, read 6, one write accepted, readback 5; 1094 ms internal, 1.2 s wall | PC image restored, no manual intervention |
| Final state | `state.json` pending null, last outcome Confirmed | — |

Calibration: on this cable/GPU configuration, **input 5 = PC** and **input 6 = laptop**. The PC's display path stayed active while the laptop input was shown, so fresh-process rediscovery works. Owner's words: "It worked perfectly."

## Full application acceptance

| Item | Status | Evidence needed |
| --- | --- | --- |
| Configuration/state/orchestrator/native backend | Implemented; fake-backend suite passes | Hardware evidence below |
| Actual Windows architecture/devices | Revalidated (above). Logi Options+ 1.93.755983, Logi Plugin Service 6.1.5.22697, Deskflow 1.27.0.0, all running | Mouse pairing and cable/GPU route still to record |
| Read-only published diagnostics | Passed (above) | — |
| Supervised native PC→laptop→PC | **Passed** 12 Nov 2026 | See section above |
| Fresh-process inactive-source gate | **Passed** 12 Nov 2026 | See section above |
| Prior LIFT assignment and selected button | Owner chose the small round button next to the scroll wheel (not Forward), assigned in All Applications. Prior assignment: Windows Task View ("Exibição de tarefas"); restore this on rollback | Any app-specific overrides |
| Probe matrix | Binding works (12 Nov 2026): owner bound the probe through Options+ UI; 3 launches logged at 15:43:35–15:43:38 UTC, separate PIDs, none left running. Press count not recorded, so exactly-once is not yet shown. **Batch A passed** (monitor PC, Deskflow focus PC): 10 counted presses → 10 log lines, 10 distinct PIDs, gaps 1.49–1.82 s, no Task View, no window or focus change, nothing left running. **Batch C passed** (monitor showing laptop via physical button): 10 presses → 10 log lines, 10 distinct PIDs, gaps 1.32–1.49 s, nothing left running | **Passed** for the owner's setup. Owner does not use Deskflow (installed and running, left unchanged); the MacBook uses its own touchpad, so Deskflow focus states B and D do not apply |
| Long hold and rapid presses | **Passed** 12 Nov 2026: ~3 s hold → exactly 1 launch, no repeat; 5 fast clicks → 5 launches 0.27–0.36 s apart, distinct PIDs, no Task View, no delayed queue, nothing left running | — |
| Real helper verification/busy lifecycle | **Passed** 12 Nov 2026: 2 presses during a running switch → `Busy` (exit 2, 0 ms), 1 press 0.5 s after completion → `Debounced` (exit 2); each in-flight switch still Confirmed, nothing queued. After a manual OSD change to the laptop, the next press read 6 and selected 5 (follows actual input) | No foreground activation, launch queue or broken later presses |
| Desktop/browser/editor profiles | Not run | Actual override behavior |
| Ten complete round trips | **Passed** 12 Nov 2026: 1 round trip from the repo build, then 15:53–15:57 UTC from the button (installed copy): 18 Confirmed toggles, all readbacks match target, typically 1078–1109 ms (one 2141 ms with an extra verification read), no pending left, no process left running. Owner: "All worked great!" | Readbacks/images, no orphan or unrelated-target writes |
| Physical OSD / reconnect / sleep/resume | Not run | Actual-state handling, identity safety and recovery |
| Laptop asleep / app missing / Options+ stopped | Not run | Clear limits and safe recovery |
| Installation and rollback | Installed 12 Nov 2026 to `%LOCALAPPDATA%\MonitorSwitch\bin`: 194/194 files match the tested manifest, app exe `390f8624…edd9`; installed `status` exit 0, read 5. Rebind to installed path and rollback demonstration pending | Package path/hash, restored assignment, unchanged Deskflow |

Use operation IDs and binary hashes to connect future evidence. Do not commit full device paths, EDID/serial dumps, unrelated window data or private screenshots. Native acceptance and a user's visual confirmation are separate evidence.
