# Local release package

The current deliverables are framework-dependent Windows x64 packages. They require the .NET 8 Desktop Runtime and are not code-signed.

| Artifact | SHA-256 |
|---|---|
| `artifacts\PodRelay-win-x64.zip` | `ED414C5F8A963E04A7FE656F21768FA3955AADC3FDF8020A9CCD901CB0A7AE46` |
| `artifacts\PodRelay-Diagnostics-win-x64.zip` | `6A9C8AADC438DB57D6A457CA9950CD302C6465D138362ED004E0FFC6B2273488` |

These SHA-256 hashes were rechecked after the 0.1.0 package refresh on 2026-07-13. The build passed 53 unit tests with zero warnings and zero errors. The application package includes `Start-PodRelay.cmd`, which detects the x64 .NET 8 Desktop Runtime and, only with user confirmation, downloads the Microsoft installer, validates its Microsoft Authenticode signature, and requests installation. PodRelay itself remains Authenticode-unsigned until the original author supplies a trusted code-signing certificate; GitHub build provenance and SHA-256 provide integrity evidence but do not suppress SmartScreen. The app also completed an isolated real-controller test: while normal automation was disabled and AirPods were on iPhone, reconnecting the bound controller restored the full Windows audio invariant in 4.28 seconds.
