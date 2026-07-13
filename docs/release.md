# Local release package

The current deliverables are framework-dependent Windows x64 packages. They require the .NET 8 Desktop Runtime and are not code-signed.

| Artifact | SHA-256 |
|---|---|
| `artifacts\PodRelay-win-x64.zip` | `C73935D1ECE78790C5F4ABE030183CAA95E60CCF052CFD3BAA34820017054711` |
| `artifacts\PodRelay-Diagnostics-win-x64.zip` | `A9FE07F3A42D0593A99678EF995224E8DC37567A874CBAD1A477A3E54DE4AC4A` |

These SHA-256 hashes are for the 0.1.1 package built on 2026-07-13. Release archives use stable file ordering and fixed ZIP entry timestamps; two consecutive local publishes produced identical hashes. The build passed 53 unit tests with zero warnings and zero errors. The application package includes `Start-PodRelay.cmd`, which detects the x64 .NET 8 Desktop Runtime and, only with user confirmation, downloads the Microsoft installer, validates its Microsoft Authenticode signature, and requests installation. PodRelay itself remains Authenticode-unsigned until the original author supplies a trusted code-signing certificate; GitHub build provenance and SHA-256 provide integrity evidence but do not suppress SmartScreen. The app also completed an isolated real-controller test: while normal automation was disabled and AirPods were on iPhone, reconnecting the bound controller restored the full Windows audio invariant in 4.28 seconds.
