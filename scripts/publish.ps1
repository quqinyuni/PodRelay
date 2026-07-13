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

& $dotnet publish "$root\src\PodRelay.App\PodRelay.App.csproj" -c Release --self-contained false --no-restore -o $output --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $dotnet publish "$root\src\PodRelay.Diagnostics\PodRelay.Diagnostics.csproj" -c Release --self-contained false --no-restore -o $diagnosticsOutput --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item -LiteralPath "$root\installer\Start-PodRelay.cmd" -Destination $output -Force
Copy-Item -LiteralPath "$root\installer\InstallRuntimeAndRun.ps1" -Destination $output -Force

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
Compress-Archive -Path "$output\*" -DestinationPath $package -CompressionLevel Optimal
Compress-Archive -Path "$diagnosticsOutput\*" -DestinationPath $diagnosticsPackage -CompressionLevel Optimal
exit 0
