$ErrorActionPreference = 'Stop'
$dotnet = if (Test-Path 'C:\tmp\podrelay-dotnet\dotnet.exe') {
    'C:\tmp\podrelay-dotnet\dotnet.exe'
} else {
    'dotnet'
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '..\.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '..\.nuget-packages'
& $dotnet build "$PSScriptRoot\..\src\PodRelay.App\PodRelay.App.csproj" -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet build "$PSScriptRoot\..\src\PodRelay.Diagnostics\PodRelay.Diagnostics.csproj" -c Release --nologo
exit $LASTEXITCODE
