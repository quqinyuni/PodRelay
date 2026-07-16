# Release packages

PodRelay releases are built on GitHub Actions from the tagged public commit. The CI workflow runs the unit tests, creates the application and diagnostics ZIP files, uploads them as a workflow artifact, and produces a GitHub build-provenance attestation for the application package.

The corresponding workflow-generated files are uploaded unchanged to GitHub Releases:

- `PodRelay-win-x64.zip`
- `PodRelay-Diagnostics-win-x64.zip`

GitHub displays a SHA-256 digest for every Release asset. The Release notes repeat those values for convenient offline verification. Fixed ZIP hashes are intentionally not committed to this file: changing a tracked hash changes the source commit that the package claims to represent.

The application ZIP includes `Start-PodRelay.cmd`, which detects the x64 .NET 8 Desktop Runtime and, only with user confirmation, downloads the Microsoft installer, validates its Microsoft Authenticode signature, and requests installation.

PodRelay itself remains Authenticode-unsigned until the original author supplies a publicly trusted code-signing certificate with an accessible private key. GitHub provenance and SHA-256 prove source and file integrity, but they do not establish a Windows publisher identity or suppress SmartScreen.

## Local release alternative

The same deterministic packages can be built and verified on Windows with `publish.cmd` followed by `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-package.ps1`. They can then be uploaded to a GitHub Release without running Actions. A local release does not receive GitHub's workflow build-provenance attestation, so its notes must identify the source commit and include the SHA-256 values of both ZIP files.

The 0.1.4 build passed 74 unit tests with zero warnings and zero errors. Its paired-device lookup resolved Windows `PID&2024` to AirPods advertisement model `0x2420`, which isolates the installed AirPods Pro 2 USB-C from nearby Pro 2 Lightning frames.
