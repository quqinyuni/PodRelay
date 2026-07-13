[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string[]] $Path,

    [Parameter(Mandatory)]
    [string] $CertificateThumbprint,

    [string] $TimestampUrl = 'https://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$thumbprint = $CertificateThumbprint.Replace(' ', '').ToUpperInvariant()
$certificate = Get-Item "Cert:\CurrentUser\My\$thumbprint" -ErrorAction SilentlyContinue
if ($null -eq $certificate) {
    throw "The Authenticode certificate $thumbprint was not found in Cert:\CurrentUser\My."
}

if (-not $certificate.HasPrivateKey) {
    throw "The Authenticode certificate $thumbprint does not have an accessible private key."
}

$signTool = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending |
    Select-Object -First 1
if ($null -eq $signTool) {
    throw 'signtool.exe was not found. Install the Windows 10/11 SDK signing tools.'
}

foreach ($item in $Path) {
    $resolved = Resolve-Path -LiteralPath $item
    & $signTool.FullName sign /sha1 $thumbprint /s My /fd SHA256 /tr $TimestampUrl /td SHA256 /d PodRelay /du https://github.com/quqinyuni/PodRelay $resolved.Path
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed to sign $($resolved.Path) with exit code $LASTEXITCODE."
    }

    & $signTool.FullName verify /pa /all $resolved.Path
    if ($LASTEXITCODE -ne 0) {
        throw "signtool verification failed for $($resolved.Path) with exit code $LASTEXITCODE."
    }
}
