param(
    [switch] $CheckOnly,
    [string] $RuntimeRoot = (Join-Path $env:ProgramFiles 'dotnet\shared\Microsoft.WindowsDesktop.App')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

$appPath = Join-Path $PSScriptRoot 'PodRelay.exe'
$runtimeRoot = $RuntimeRoot
$downloadUrl = 'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe'
$downloadPage = 'https://dotnet.microsoft.com/download/dotnet/8.0'

function Test-DesktopRuntime8 {
    if (-not (Test-Path -LiteralPath $runtimeRoot)) {
        return $false
    }

    foreach ($directory in Get-ChildItem -LiteralPath $runtimeRoot -Directory -ErrorAction SilentlyContinue) {
        $version = $null
        if ([Version]::TryParse($directory.Name.Split('-')[0], [ref]$version) -and $version.Major -eq 8) {
            return $true
        }
    }

    return $false
}

function Show-Error([string] $message) {
    [System.Windows.Forms.MessageBox]::Show(
        $message,
        'PodRelay',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
}

try {
    if (-not (Test-Path -LiteralPath $appPath)) {
        throw 'PodRelay.exe is missing. Extract the complete release package and try again.'
    }

    if (Test-DesktopRuntime8) {
        if ($CheckOnly) {
            exit 0
        }

        Start-Process -FilePath $appPath -WorkingDirectory $PSScriptRoot
        exit 0
    }

    if ($CheckOnly) {
        exit 3
    }

    $choice = [System.Windows.Forms.MessageBox]::Show(
        ".NET 8 Desktop Runtime (x64) is not installed.`n`nPodRelay can download and install it from Microsoft. Continue?",
        'PodRelay runtime required',
        [System.Windows.Forms.MessageBoxButtons]::YesNo,
        [System.Windows.Forms.MessageBoxIcon]::Information)
    if ($choice -ne [System.Windows.Forms.DialogResult]::Yes) {
        exit 2
    }

    $installerPath = Join-Path $env:TEMP 'windowsdesktop-runtime-8.0-win-x64.exe'
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $downloadUrl -OutFile $installerPath -UseBasicParsing
        $signature = Get-AuthenticodeSignature -LiteralPath $installerPath
        if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or
            $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') {
            throw "The Microsoft runtime installer signature is not valid ($($signature.Status)). The installer will not run."
        }

        $process = Start-Process -FilePath $installerPath `
            -ArgumentList '/install', '/quiet', '/norestart' `
            -Verb RunAs `
            -Wait `
            -PassThru
        if ($process.ExitCode -notin 0, 3010) {
            throw "The Microsoft runtime installer failed with exit code $($process.ExitCode)."
        }
    }
    finally {
        if (Test-Path -LiteralPath $installerPath) {
            Remove-Item -LiteralPath $installerPath -Force -ErrorAction SilentlyContinue
        }
    }

    if (-not (Test-DesktopRuntime8)) {
        throw '.NET 8 Desktop Runtime installation finished, but the runtime is still not detected.'
    }

    Start-Process -FilePath $appPath -WorkingDirectory $PSScriptRoot
    exit 0
}
catch {
    Show-Error "$($_.Exception.Message)`n`nYou can also install the runtime manually from Microsoft:`n$downloadPage"
    Start-Process $downloadPage
    exit 1
}
