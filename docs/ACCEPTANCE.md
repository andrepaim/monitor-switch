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

## Full application acceptance — all pending

| Item | Status | Evidence needed |
| --- | --- | --- |
| Configuration/state/orchestrator/native backend | Not implemented | Unit and fake-backend suite, interruption/disposal evidence |
| Actual Windows architecture/software/devices | Not revalidated | Date, OS/architecture, Options+/Deskflow versions, identity/cable/GPU route |
| Read-only published diagnostics | Not run | Binary SHA-256 and structured output |
| Supervised native PC→laptop→PC | Not run | API readbacks and owner-confirmed images |
| Fresh-process inactive-source gate | Not run | Old process exited, handles released, new discovery/read/restore |
| Prior LIFT assignment and selected button | Not inspected | Screenshot/plain text and owner choice |
| Probe matrix | Not run | Ten presses/ten executions in each relevant focus/image state |
| Long hold and rapid presses | Not run | No held repeat, oscillation, forwarding or delayed queue |
| Real helper verification/busy lifecycle | Not run | No foreground activation, launch queue or broken later presses |
| Desktop/browser/editor profiles | Not run | Actual override behavior |
| Ten complete round trips | Not run | Readbacks/images, no orphan or unrelated-target writes |
| Physical OSD / reconnect / sleep/resume | Not run | Actual-state handling, identity safety and recovery |
| Laptop asleep / app missing / Options+ stopped | Not run | Clear limits and safe recovery |
| Installation and rollback | Not run | Package path/hash, restored assignment, unchanged Deskflow |

Use operation IDs and binary hashes to connect future evidence. Do not commit full device paths, EDID/serial dumps, unrelated window data or private screenshots. Native acceptance and a user's visual confirmation are separate evidence.
