param(
    [string] $Root = (Resolve-Path "$PSScriptRoot\..")
)

$ErrorActionPreference = 'Stop'

$publishDirectory = Join-Path $Root 'artifacts\publish\win-x64'
$diagnosticsDirectory = Join-Path $Root 'artifacts\diagnostics\win-x64'
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

[xml]$buildProperties = Get-Content -LiteralPath (Join-Path $Root 'Directory.Build.props') -Raw
$expectedProductVersion = [string]$buildProperties.Project.PropertyGroup.Version
$expectedAssemblyVersion = [string]$buildProperties.Project.PropertyGroup.AssemblyVersion
if ([string]::IsNullOrWhiteSpace($expectedProductVersion) -or
    [string]::IsNullOrWhiteSpace($expectedAssemblyVersion)) {
    throw 'Directory.Build.props does not declare Version and AssemblyVersion.'
}

$assemblies = @(
    (Join-Path $publishDirectory 'PodRelay.dll'),
    (Join-Path $publishDirectory 'PodRelay.Core.dll'),
    (Join-Path $diagnosticsDirectory 'PodRelay.Diagnostics.dll'),
    (Join-Path $diagnosticsDirectory 'PodRelay.Core.dll')
)
foreach ($assemblyPath in $assemblies) {
    if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
        throw "Required release assembly is missing: $assemblyPath"
    }

    $actualVersion = [Reflection.AssemblyName]::GetAssemblyName($assemblyPath).Version.ToString()
    if ($actualVersion -ne $expectedAssemblyVersion) {
        throw "Assembly version mismatch for $assemblyPath`: expected $expectedAssemblyVersion, found $actualVersion."
    }
}

$appDependencies = Get-Content -LiteralPath (Join-Path $publishDirectory 'PodRelay.deps.json') -Raw
$diagnosticDependencies = Get-Content -LiteralPath (Join-Path $diagnosticsDirectory 'PodRelay.Diagnostics.deps.json') -Raw
foreach ($dependency in @(
    @{ Name = 'PodRelay'; Text = $appDependencies },
    @{ Name = 'PodRelay.Core'; Text = $appDependencies },
    @{ Name = 'PodRelay.Diagnostics'; Text = $diagnosticDependencies },
    @{ Name = 'PodRelay.Core'; Text = $diagnosticDependencies }
)) {
    $expectedEntry = '"' + $dependency.Name + '/' + $expectedProductVersion + '"'
    if ($dependency.Text.IndexOf($expectedEntry, [StringComparison]::Ordinal) -lt 0) {
        throw "Dependency manifest is missing the expected entry $expectedEntry."
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
Write-Host "Verified release assembly and dependency versions: $expectedProductVersion."
Write-Host 'Verified launcher branches: runtime present = 0; runtime missing = 3.'
exit 0
