# Windows setup and calibration

**Current state: scaffold only. Do not bind the main executable as a finished switch.** Monitor commands currently return exit 21 and do not emit structured result files. The example config contains placeholders and is not enrollment.

## Development build

Use .NET 10 SDK and PowerShell 7+ for scripts. Build/test as described in the README. Inspect the Windows OS and architecture before selecting the publish runtime:

```powershell
[System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
.\scripts\Publish.ps1 -Runtime win-x64
```

Use `win-arm64` instead if verified appropriate. The script produces a self-contained folder for each executable; copy the whole folder when testing. No runtime installation is required on the eventual target. A single-file package is optional future packaging work.

## Harmless probe (implemented development utility)

Manual Windows check, from the repository root after publishing:

```powershell
$probeExe = (Resolve-Path .\artifacts\win-x64\probe\MonitorSwitch.TriggerProbe.exe).Path
$probeProcess = Start-Process -FilePath $probeExe -Wait -PassThru
$probeProcess.ExitCode
Get-Content "$env:LOCALAPPDATA\MonitorSwitch\logs\trigger-probe.jsonl" -Tail 10
```

The probe only writes timestamp/PID/version/outcome records; it does not touch DDC, config, switching state or Deskflow. Exit 0 means an entry was flushed; 20 means local logging failed; the dedicated probe rejects arguments with 10. Log rotation/retention is not yet implemented: remove test logs explicitly if no longer needed. Do not treat successful manual execution as proof of Options+/Deskflow integration.

## Later implementation setup (pending)

1. Recheck monitor, mouse pairing, architecture, Options+/Deskflow versions and roles, GPU/cable path, and app overrides. Record only relevant evidence.
2. Implement and run read-only discovery. Select the exact device path and EDID hash. Explicit enrollment must validate SAM/SAM1017 and unique association. Never copy the example config as valid enrollment.
3. Tell the owner before a physical test. Have the monitor's physical input control available and record the original input. Test explicit switch/readback plus owner-confirmed images.
4. Prove restoration from a new process after every old physical handle has been disposed while the laptop image is selected.
5. Show the actual LIFT button diagram and existing assignment. Propose Forward; confirm selection before changing it. Save screenshot/plain text and supported export if available. Save evidence outside Git.
6. Through supported Options+ UI, temporarily assign the dedicated probe in All Applications. Inspect actual Open Application/Open File or Smart Action options. Do not silently create/sign into an account or modify private databases.
7. Run ten separated presses in every design matrix state, desktop/browser/editor checks, rapid presses, and long hold. Verify no duplicate launch, repeat while held, Back/Forward forwarding, or focus change.
8. Only after the probe gate passes, bind the real implemented executable and repeat lifecycle checks while it verifies/is busy. Record native and visual results separately.
9. Install only the tested complete package to the approved stable per-user directory. Retain one prior package/config on update. Preserve enrollment and the button choice.

The physical monitor input button is the recovery path. Never perform blind automatic rollback. If a native call hangs, keep the writer isolated; manual termination leaves an uncertain outcome, which the completed implementation must reconcile on the next invocation without toggling.

## Screenshots to capture during actual setup

- LIFT button diagram and previous assignment.
- Selected supported Options+ action and absolute executable path.
- Relevant All Applications/app-specific override settings.
- Supported backup/export and restore controls, if present.

No screenshots or Windows observations have been fabricated for this scaffold. See [acceptance](ACCEPTANCE.md) for pending gates and [rollback](ROLLBACK.md) for removal.
