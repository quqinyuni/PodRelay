param(
    [string] $Root = (Resolve-Path "$PSScriptRoot\..")
)

$ErrorActionPreference = 'Stop'

$publishDirectory = Join-Path $Root 'artifacts\publish\win-x64'
$packagePath = Join-Path $Root 'artifacts\PodRelay-win-x64.zip'
$launcherPath = Join-Path $publishDirectory 'InstallRuntimeAndRun.ps1'
$runtimeConfigPath = Join-Path $publishDirectory 'PodRelay.runtimeconfig.json'

foreach ($requiredPath in @(
    (Join-Path $publishDirectory 'PodRelay.exe'),
    (Join-Path $publishDirectory 'Start-PodRelay.cmd'),
    $launcherPath,
    $runtimeConfigPath,
    $packagePath
)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required release artifact is missing: $requiredPath"
    }
}

$runtimeConfig = Get-Content -LiteralPath $runtimeConfigPath -Raw | ConvertFrom-Json
$desktopFramework = @(
    @($runtimeConfig.runtimeOptions.frameworks) |
        Where-Object { $_.name -eq 'Microsoft.WindowsDesktop.App' }
)
if ($desktopFramework.Count -ne 1 -or -not ([string]$desktopFramework[0].version).StartsWith('8.')) {
    throw 'PodRelay.runtimeconfig.json does not require Microsoft.WindowsDesktop.App 8.x.'
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $entryNames = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
}
finally {
    $archive.Dispose()
}

foreach ($requiredEntry in @('PodRelay.exe', 'Start-PodRelay.cmd', 'InstallRuntimeAndRun.ps1')) {
    if ($entryNames -notcontains $requiredEntry) {
        throw "Release ZIP is missing required entry: $requiredEntry"
    }
}

$bundledRuntimeFiles = @($entryNames | Where-Object {
    [IO.Path]::GetFileName($_) -in @('coreclr.dll', 'clrjit.dll', 'hostfxr.dll', 'hostpolicy.dll') -or
    $_ -match '(^|/)shared/Microsoft\.(NETCore|WindowsDesktop)\.App/'
})
if ($bundledRuntimeFiles.Count -gt 0) {
    throw "Release ZIP unexpectedly bundles .NET runtime files: $($bundledRuntimeFiles -join ', ')"
}

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $launcherPath -CheckOnly
if ($LASTEXITCODE -ne 0) {
    throw "Launcher did not detect the installed .NET 8 Desktop Runtime (exit $LASTEXITCODE)."
}

$missingRuntimeRoot = Join-Path $env:TEMP "PodRelay-Missing-Runtime-$PID"
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $launcherPath -CheckOnly -RuntimeRoot $missingRuntimeRoot
if ($LASTEXITCODE -ne 3) {
    throw "Launcher missing-runtime check returned $LASTEXITCODE instead of 3."
}

Write-Host "Verified framework-dependent release package ($($entryNames.Count) entries)."
Write-Host 'Verified launcher branches: runtime present = 0; runtime missing = 3.'
