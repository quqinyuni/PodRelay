$ErrorActionPreference = 'Stop'
$dotnet = if (Test-Path 'C:\tmp\podrelay-dotnet\dotnet.exe') {
    'C:\tmp\podrelay-dotnet\dotnet.exe'
} else {
    'dotnet'
}
$root = Resolve-Path "$PSScriptRoot\.."
$output = Join-Path $root 'artifacts\publish\win-x64'
$diagnosticsOutput = Join-Path $root 'artifacts\diagnostics\win-x64'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $root '.nuget-packages'

function New-DeterministicZip {
    param(
        [Parameter(Mandatory)] [string] $SourceDirectory,
        [Parameter(Mandatory)] [string] $DestinationPath
    )

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $sourcePath = (Resolve-Path -LiteralPath $SourceDirectory).Path.TrimEnd('\')
    $fixedTimestamp = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
    $fileStream = [IO.File]::Open($DestinationPath, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
    $archive = [IO.Compression.ZipArchive]::new($fileStream, [IO.Compression.ZipArchiveMode]::Create, $false)
    try {
        foreach ($item in Get-ChildItem -LiteralPath $sourcePath -File -Recurse | Sort-Object FullName) {
            $relativePath = $item.FullName.Substring($sourcePath.Length).TrimStart([char]'\', [char]'/').Replace('\', '/')
            $entry = $archive.CreateEntry($relativePath, [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = $fixedTimestamp
            $input = $item.OpenRead()
            $outputStream = $entry.Open()
            try {
                $input.CopyTo($outputStream)
            }
            finally {
                $outputStream.Dispose()
                $input.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
        $fileStream.Dispose()
    }
}

function Copy-NormalizedTextFile {
    param(
        [Parameter(Mandatory)] [string] $SourcePath,
        [Parameter(Mandatory)] [string] $DestinationPath
    )

    $utf8WithoutBom = [Text.UTF8Encoding]::new($false)
    $text = [IO.File]::ReadAllText($SourcePath, [Text.Encoding]::UTF8)
    $text = $text.Replace("`r`n", "`n").Replace("`r", "`n")
    [IO.File]::WriteAllText($DestinationPath, $text, $utf8WithoutBom)
}

& $dotnet restore "$root\src\PodRelay.App\PodRelay.App.csproj" --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $dotnet restore "$root\src\PodRelay.Diagnostics\PodRelay.Diagnostics.csproj" --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $dotnet publish "$root\src\PodRelay.App\PodRelay.App.csproj" -c Release --self-contained false --no-restore -o $output --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $dotnet publish "$root\src\PodRelay.Diagnostics\PodRelay.Diagnostics.csproj" -c Release --self-contained false --no-restore -o $diagnosticsOutput --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-NormalizedTextFile -SourcePath "$root\installer\Start-PodRelay.cmd" -DestinationPath (Join-Path $output 'Start-PodRelay.cmd')
Copy-NormalizedTextFile -SourcePath "$root\installer\InstallRuntimeAndRun.ps1" -DestinationPath (Join-Path $output 'InstallRuntimeAndRun.ps1')

$signingThumbprint = $env:PODRELAY_SIGNING_THUMBPRINT
if (-not [string]::IsNullOrWhiteSpace($signingThumbprint)) {
    & "$PSScriptRoot\sign.ps1" -Path @(
        (Join-Path $output 'PodRelay.exe'),
        (Join-Path $diagnosticsOutput 'PodRelay.Diagnostics.exe')
    ) -CertificateThumbprint $signingThumbprint
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} else {
    Write-Warning 'PODRELAY_SIGNING_THUMBPRINT is not set; packages will be Authenticode-unsigned.'
}

$package = Join-Path $root 'artifacts\PodRelay-win-x64.zip'
$diagnosticsPackage = Join-Path $root 'artifacts\PodRelay-Diagnostics-win-x64.zip'
if (Test-Path -LiteralPath $package) { Remove-Item -LiteralPath $package }
if (Test-Path -LiteralPath $diagnosticsPackage) { Remove-Item -LiteralPath $diagnosticsPackage }
New-DeterministicZip -SourceDirectory $output -DestinationPath $package
New-DeterministicZip -SourceDirectory $diagnosticsOutput -DestinationPath $diagnosticsPackage
exit 0
