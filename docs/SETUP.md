# Windows setup and calibration

**Current state: all steps below completed on the target PC on 12 November 2026; the button is bound to the installed copy in `%LOCALAPPDATA%\MonitorSwitch\bin`. On a new setup, do not bind a mouse button to the main executable until the supervised switch, fresh-process gate and probe matrix pass.**

All examples run from the repository root in PowerShell 7+. MonitorSwitch is a WinExe, so collect exit codes with `Start-Process -Wait -PassThru`.

```powershell
$exe = (Resolve-Path .\artifacts\win-x64\app\MonitorSwitch.exe).Path
function Invoke-MonitorSwitch { (Start-Process -FilePath $exe -ArgumentList $args -Wait -PassThru).ExitCode }
```

## 1. Build and package

```powershell
[System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture   # X64 on this PC
.\scripts\Publish.ps1 -Runtime win-x64
```

Copy the whole folder when testing; an apphost alone is not the package.

## 2. Read-only discovery and explicit enrollment

```powershell
Invoke-MonitorSwitch discover --json --output "$env:TEMP\ms-discover.json"
Get-Content "$env:TEMP\ms-discover.json"
```

Check there is exactly one `SAM1017` candidate with `problem: null`. Enroll by copying its exact `deviceInterfacePath` and `edidIdentityHash` (quote the path; it contains `&`):

```powershell
Invoke-MonitorSwitch enroll --device-path '"<deviceInterfacePath>"' --edid-hash <edidIdentityHash>
Get-Content "$env:LOCALAPPDATA\MonitorSwitch\last-result.json"
```

Re-enrollment needs `--replace`, refuses while a pending operation exists, and backs up the old config to `config.previous.json`. Never copy `config.example.json` as enrollment.

Confirm read-only behavior before any write:

```powershell
Invoke-MonitorSwitch status --output "$env:TEMP\ms-status.json"
Invoke-MonitorSwitch toggle --dry-run      # plans only; see last-result.json
```

## 3. Supervised physical switch (owner present)

Recovery path: the monitor's own input button/OSD. Note the current image and input first. Labels are cosmetic; record which physical source (PC or laptop) each value shows.

```powershell
Invoke-MonitorSwitch set --input 6   # expect laptop image; exit 0 = target read back
Get-Content "$env:LOCALAPPDATA\MonitorSwitch\last-result.json"
Invoke-MonitorSwitch set --input 5   # expect PC image
```

Record exit code, `before/target/observed`, `write.apiAccepted`, elapsed time, and what the owner actually sees. API acceptance is not visual success.

## 4. Fresh-process inactive-source gate

With input 6 (laptop) selected by step 3 and that process exited, every handle has been released. A new launch must rediscover the monitor and restore the PC:

```powershell
Get-Process MonitorSwitch -ErrorAction SilentlyContinue   # must be empty
Invoke-MonitorSwitch set --input 6
Get-Process MonitorSwitch -ErrorAction SilentlyContinue   # must be empty
Invoke-MonitorSwitch toggle                               # new process: expect PC image, exit 0
```

If discovery fails while the laptop input is shown (exit 11), the PC's display path disappeared: record it and follow design §10. Do not add a resident handle keeper.

## 5. Harmless probe and button selection

```powershell
$probeExe = (Resolve-Path .\artifacts\win-x64\probe\MonitorSwitch.TriggerProbe.exe).Path
(Start-Process -FilePath $probeExe -Wait -PassThru).ExitCode
Get-Content "$env:LOCALAPPDATA\MonitorSwitch\logs\trigger-probe-$((Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')).jsonl" -Tail 10
```

Exit 0 means an entry was flushed, 20 means local logging failed, 10 means arguments were passed. Logs rotate at 1 MB and are kept seven days.

1. Recheck mouse pairing, Options+/Deskflow versions and roles, and app-specific profiles.
2. Show the actual LIFT button diagram and existing assignment. Propose Forward; confirm the owner's choice before changing it. Save a screenshot/plain-text record and any supported export outside Git.
3. Through supported Options+ UI, assign the probe executable's absolute path in All Applications (Open Application/Open File, else Smart Action → Application Open). Do not silently create/sign into an account or modify private databases.
4. Run ten separated presses in every design §8 state, desktop/browser/editor checks, rapid presses and a long hold. Count log lines; verify no duplicates, repeats while held, Back/Forward forwarding or focus change.

## 6. Bind the real switch

Only after the probe gate passes, replace the probe with the absolute path of `MonitorSwitch.exe` (no arguments). Repeat lifecycle checks while it verifies or is busy, then ten complete round trips. Record native and visual results separately in [acceptance](ACCEPTANCE.md).

Install only the tested complete package to the approved stable per-user directory (`%LOCALAPPDATA%\MonitorSwitch\bin`). Update only with no operation active, keeping one prior package/config.

## Recovery

The physical monitor input button always works. Never perform blind automatic rollback. If a native call hangs, the process keeps the lock; terminating it leaves a pending record that the next launch reconciles read-only (exit 16 or 15) without switching. Press again after a reconciliation to switch.

## Screenshots to capture during actual setup

- LIFT button diagram and previous assignment.
- Selected supported Options+ action and absolute executable path.
- Relevant All Applications/app-specific override settings.
- Supported backup/export and restore controls, if present.
