#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('win-x64', 'win-arm64')]
    [string] $Runtime
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    & (Join-Path $PSScriptRoot 'Build.ps1')

    $targets = @(
        @{ Name = 'app'; Project = 'src/MonitorSwitch/MonitorSwitch.csproj' },
        @{ Name = 'probe'; Project = 'src/MonitorSwitch.TriggerProbe/MonitorSwitch.TriggerProbe.csproj' }
    )
    foreach ($target in $targets) {
        $publishDirectory = Join-Path $projectRoot "artifacts/$Runtime/$($target.Name)"
        # A normal self-contained folder avoids single-file extraction assumptions.
        & dotnet publish $target.Project -c Release -r $Runtime --self-contained true `
            -p:DebugType=None -p:DebugSymbols=false -o $publishDirectory
        if ($LASTEXITCODE -ne 0) { throw "Publish failed: $($target.Name)" }

        $manifestPath = Join-Path $projectRoot "artifacts/$Runtime/$($target.Name)-SHA256SUMS.txt"
        Get-ChildItem -LiteralPath $publishDirectory -File -Recurse |
            Sort-Object FullName |
            ForEach-Object {
                $relativePath = [IO.Path]::GetRelativePath($publishDirectory, $_.FullName)
                $digest = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                "$digest  $relativePath"
            } | Set-Content -LiteralPath $manifestPath -Encoding utf8
        Write-Output "Development package: $publishDirectory"
        Write-Output "SHA-256 manifest: $manifestPath"
    }
    Write-Output 'Scaffold only: monitor commands return exit 21. Nothing was installed or bound.'
}
finally {
    Pop-Location
}
