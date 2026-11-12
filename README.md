# MonitorSwitch

An on-demand Windows helper for switching André's Samsung LU28R55 between its tested input values 5 and 6 using a Logitech LIFT button and Logi Options+.

**Status: design v1.1 and project scaffold. Native switching is not implemented or accepted.**

The first project file was [the v1.1 design](docs/DESIGN-v1.1.md). It includes the reviewed fresh-process recovery gate, trigger matrix, hold behavior, durable state contract, enrollment contract and failure semantics.

## Included

- .NET 10 solution with a Windows WinExe, pure core, Windows backend boundary, shared file/probe utilities, dedicated harmless trigger probe, and xUnit test project.
- Strict command parsing, fixed 0x60/5/6 device profile, operation and persistence contracts, and a fake monitor fixture.
- Build/test and self-contained Windows publish scripts, sample configuration, CI, setup/rollback documents and an acceptance ledger.

The main executable returns **21 (NotImplemented)** for all valid monitor commands, including discovery, status, enrollment and dry-run. It does not create reports or access monitors for those commands yet. Invalid commands return 10. No native DDC entry points exist in this scaffold.

`probe-trigger` and the dedicated no-argument `MonitorSwitch.TriggerProbe.exe` append a harmless JSON line to `%LOCALAPPDATA%\MonitorSwitch\logs\trigger-probe.jsonl`. They do not read enrollment or switching state. Probe log rotation/retention is pending, so this is a development probe, not a finished installed component. No button is assigned automatically.

## Build and test

To continue development on Windows, clone the public repository from PowerShell:

```powershell
git clone https://github.com/andrepaim/monitor-switch.git
Set-Location monitor-switch
```

Install a supported .NET 10 SDK. From the repository root:

```powershell
dotnet restore MonitorSwitch.slnx
dotnet build MonitorSwitch.slnx -c Release --no-restore
dotnet test MonitorSwitch.slnx -c Release --no-build
# Or, in PowerShell 7+:
.\scripts\Build.ps1
```

The pure tests run on Linux and Windows; Windows apps can be cross-built on Linux but must be exercised on the actual Windows PC. Tests currently cover command parsing and device-profile safety only. They do not prove the future orchestration, persistence, native backend or physical trigger works.

On Windows, after confirming the machine architecture, create a development package:

```powershell
.\scripts\Publish.ps1 -Runtime win-x64
```

This publishes separate self-contained app/probe folders under `artifacts` and records SHA-256 manifests. It does not install, enroll, bind, or switch anything. See [setup](docs/SETUP.md) before running on hardware.

## Project layout

| Path | Purpose |
| --- | --- |
| `src/MonitorSwitch` | Silent Windows command boundary |
| `src/MonitorSwitch.Core` | Pure contracts, command parser, fixed device profile |
| `src/MonitorSwitch.Windows` | Explicitly unimplemented native locator boundary |
| `src/MonitorSwitch.Runtime` | Per-user paths and harmless probe file I/O; no native API dependency |
| `src/MonitorSwitch.TriggerProbe` | Dedicated no-argument probe executable |
| `tests/MonitorSwitch.Tests` | Hardware-free tests and fake session |
| `config.example.json` | Illustrative enrollment shape; not valid enrollment |

Continue with [implementation milestones](docs/IMPLEMENTATION.md). Record measured results in [acceptance](docs/ACCEPTANCE.md). Restore settings and remove only app-owned files using [rollback](docs/ROLLBACK.md).

No license has been selected. The source is published at https://github.com/andrepaim/monitor-switch. No installation, service, mouse assignment, or Deskflow change is part of this scaffold.
