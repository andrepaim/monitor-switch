# Implementation milestones

The requested design/scaffold deliverable is separate from implementing the full application. Checked items exist in this repository; unchecked items remain work, not implicit acceptance.

## Foundation

- [x] Write the complete revised v1.1 design before scaffolding.
- [x] Initialize standalone Git repository on branch `main`.
- [x] Create .NET 10 projects, references, WinExe boundaries and deterministic build settings.
- [x] Add strict command parsing, fixed device profile and pure operation contracts.
- [x] Add hardware-free command/profile tests and fake-session fixture.
- [x] Add harmless probe entry points independent of monitor/config/state access.
- [x] Add sample config, build/publish scripts, CI and setup/rollback/acceptance documentation.

## Read-only implementation

- [ ] Strict config deserialization/validation, including unknown/duplicate fields and bounds.
- [ ] Structured command reports, output-path protection and last-result coordination.
- [ ] DisplayConfig/GDI/EDID mapping, exact unique native locator and handle ownership.
- [ ] Read-only discover/status/diagnose and explicit enrollment/re-enrollment.
- [ ] Complete EDID validation and documented SHA-256 serialization.
- [ ] Probe rotation, seven-day retention, and concurrency/failure tests.

## Switching implementation

- [ ] Nonblocking operation lock covering all sessions sharing the state directory.
- [ ] Atomic authoritative state store and interruption tests.
- [ ] Boot-aware cooldown, pending reconciliation, injected clock/delay and result reporting.
- [ ] Pure orchestrator/fake-backend suite covering every v1.1 failure branch.
- [ ] Reviewed native interop, backend input guards and disposal tests.
- [ ] Exactly one native write, bounded readback and same-identity re-enumeration.
- [ ] Optional non-focus-stealing error feedback and bounded local diagnostic logs.
- [ ] Retain matching release symbols separately and verify final packaging/signing status; scaffold publish currently omits symbols.

## Windows and physical acceptance

- [ ] Revalidate actual host architecture, software/devices and cable/GPU path.
- [ ] Read-only published-binary diagnostics on enrolled monitor.
- [ ] Supervised native switch with owner-confirmed images and physical recovery.
- [ ] Fresh-process inactive-source rediscovery and restore gate.
- [ ] Show selected button and prior assignment; preserve rollback evidence.
- [ ] Probe matrix across Deskflow focus states and long-hold behavior.
- [ ] Real helper lifecycle tests while verifying/blocked; no foreground action or queue.
- [ ] All hardware acceptance checks from the design, including ten round trips.
- [ ] Approved per-user installation and exact rollback demonstration.

The Windows locator deliberately throws before acquiring any handles. The host deliberately returns exit 21 for monitor commands. Introduce native reads before writes and replace each scaffold boundary only after its tests and failure semantics are implemented.
