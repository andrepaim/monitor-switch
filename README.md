# MonitorSwitch

An on-demand Windows helper for switching André's Samsung LU28R55 between its tested input values 5 and 6 using a Logitech LIFT button and Logi Options+.

**Status: implemented and verified read-only on the target PC. Physical switching, the fresh-process gate and the button/Deskflow matrix still need supervised acceptance; see [acceptance](docs/ACCEPTANCE.md).**

The implementation follows [the v1.1 design](docs/DESIGN-v1.1.md): the fresh-process recovery gate, trigger matrix, hold behavior, durable state contract, enrollment contract and failure semantics.

## What it does

`MonitorSwitch.exe` with no arguments (or `toggle`) runs once and exits:

1. Takes a nonblocking per-user operation lock (a second launch returns Busy instead of queueing).
2. Reconciles any pending operation left by an interrupted run, read-only, and stops there.
3. Applies the cooldown, rediscovers the enrolled monitor by exact device path and EDID hash, and reads VCP 0x60.
4. Saves a pending record, calls `SetVCPFeature(0x60, 5↔6)` exactly once, and polls until the target is observed or the deadline passes.
5. Atomically saves the outcome to `state.json`, writes `last-result.json` and a JSONL log line, releases handles and the lock.

Other commands: `discover --json --output PATH`, `status --output PATH`, `diagnose --output PATH`, `enroll --device-path PATH --edid-hash HASH [--replace]`, `set --input 5|6`, `toggle --dry-run`, `probe-trigger`. Exit codes are in design §7.

`MonitorSwitch.TriggerProbe.exe` (no arguments) only appends a JSON line to `%LOCALAPPDATA%\MonitorSwitch\logs\trigger-probe-YYYY-MM-DD.jsonl`, for proving the button fires once per press before binding the real switch. No button is assigned automatically.

## Build and test

Install a supported .NET 10 SDK. From the repository root in PowerShell 7+:

```powershell
.\scripts\Build.ps1          # restore, build, hardware-free tests
.\scripts\Publish.ps1 -Runtime win-x64
```

Tests use deterministic fakes and never touch hardware. They cover command parsing, config/EDID validation, every orchestrator branch (including interruption at each persistence boundary), read-only commands, enrollment, the state store, lock, logs and probe. They do not prove the physical switch or the trigger works.

`Publish.ps1` writes separate self-contained `app` and `probe` folders under `artifacts` with SHA-256 manifests. It does not install, enroll, bind, or switch anything. See [setup](docs/SETUP.md) before running on hardware.

## Project layout

| Path | Purpose |
| --- | --- |
| `src/MonitorSwitch` | Silent WinExe command boundary |
| `src/MonitorSwitch.Core` | Contracts, command parser, config/EDID validation, orchestrator, read-only commands |
| `src/MonitorSwitch.Windows` | DisplayConfig/GDI/SetupAPI discovery, dxva2 DDC session, boot-aware clock |
| `src/MonitorSwitch.Runtime` | Per-user paths, atomic files, state/config stores, file lock, logs, reports, probe |
| `src/MonitorSwitch.TriggerProbe` | Dedicated no-argument probe executable |
| `tests/MonitorSwitch.Tests` | Hardware-free tests and fakes |
| `config.example.json` | Illustrative enrollment shape; not valid enrollment |

Record measured results in [acceptance](docs/ACCEPTANCE.md). Restore settings and remove only app-owned files using [rollback](docs/ROLLBACK.md).

No license has been selected. The source is published at https://github.com/andrepaim/monitor-switch. No service, mouse assignment, or Deskflow change is made by building or publishing.
