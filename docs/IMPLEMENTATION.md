# Implementation milestones

Checked items exist in this repository; unchecked items remain work, not implicit acceptance.

## Foundation

- [x] Write the complete revised v1.1 design before scaffolding.
- [x] Initialize standalone Git repository on branch `main`.
- [x] Create .NET 10 projects, references, WinExe boundaries and deterministic build settings.
- [x] Add strict command parsing, fixed device profile and pure operation contracts.
- [x] Add hardware-free command/profile tests and fake-session fixture.
- [x] Add harmless probe entry points independent of monitor/config/state access.
- [x] Add sample config, build/publish scripts, CI and setup/rollback/acceptance documentation.

## Read-only implementation

- [x] Strict config deserialization/validation, including unknown/duplicate fields and bounds (`Core/Config.cs`).
- [x] Structured command reports, output-path protection and last-result coordination (`Runtime/Reporting.cs`).
- [x] DisplayConfig/GDI/EDID mapping, exact unique native locator and handle ownership (`Windows/`).
- [x] Read-only discover/status/diagnose and explicit enrollment/re-enrollment (`Core/MaintenanceCommands.cs`).
- [x] Complete EDID validation and documented SHA-256 serialization (`Core/Edid.cs`: all blocks, checksums, lowercase hex).
- [x] Probe rotation, seven-day retention, and concurrency/failure tests.

## Switching implementation

- [x] Nonblocking operation lock covering all sessions sharing the state directory (file lock; design §7).
- [x] Atomic authoritative state store and interruption tests.
- [x] Boot-aware cooldown (Windows `BootId` + `GetTickCount64`), pending reconciliation, injected clock/delay and result reporting.
- [x] Pure orchestrator/fake-backend suite covering every v1.1 failure branch.
- [x] Reviewed native interop with runtime layout checks, backend input guards and single-owner disposal.
- [x] Exactly one native write, bounded readback and same-identity re-enumeration.
- [x] Optional non-focus-stealing error feedback (`MessageBeep`) and bounded local diagnostic logs.
- [ ] Retain matching release symbols separately and verify final packaging/signing status; development publish currently omits symbols and is unsigned.

## Windows and physical acceptance

- [x] Revalidate actual host architecture and monitor identity (Windows 11 Pro x64, RTX 3060, one SAM1017).
- [x] Read-only published-binary diagnostics on enrolled monitor.
- [ ] Revalidate Options+/Deskflow versions, mouse pairing and cable/GPU path.
- [x] Supervised native switch with owner-confirmed images and physical recovery (input 5 = PC, 6 = laptop).
- [x] Fresh-process inactive-source rediscovery and restore gate.
- [x] Show selected button and prior assignment; preserve rollback evidence (round button by the wheel; prior: Task View).
- [x] Probe matrix across image states and long-hold behavior (owner does not use Deskflow).
- [ ] Real helper lifecycle tests while verifying/blocked; no foreground action or queue.
- [ ] All hardware acceptance checks from the design, including ten round trips.
- [ ] Approved per-user installation and exact rollback demonstration.

Native code is exercised only in supervised Windows sessions; automated tests use fakes. A blocked native DDC call cannot be cancelled: the process keeps the lock until it returns or is terminated, and the next launch reconciles.
