$ErrorActionPreference = 'Stop'
$dotnet = if (Test-Path 'C:\tmp\podrelay-dotnet\dotnet.exe') {
    'C:\tmp\podrelay-dotnet\dotnet.exe'
} else {
    'dotnet'
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '..\.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '..\.nuget-packages'
& $dotnet test "$PSScriptRoot\..\tests\PodRelay.Core.Tests\PodRelay.Core.Tests.csproj" -c Release --nologo
exit $LASTEXITCODE
