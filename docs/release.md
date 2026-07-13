# Local release package

The current deliverables are framework-dependent Windows x64 packages. They require the .NET 8 Desktop Runtime and are not code-signed.

| Artifact | SHA-256 |
|---|---|
| `artifacts\PodRelay-win-x64.zip` | `3BD6D41B4E3AF156D81A334E09EBD90CAB443C3B5147953A3EA98D6C240D6510` |
| `artifacts\PodRelay-Diagnostics-win-x64.zip` | `7140801B55A98B0F11B1DCCD1B2CC4139A5790535BE124B2382E70CBC5864970` |

These SHA-256 hashes are for the 0.1.1 package built on 2026-07-13. The build passed 53 unit tests with zero warnings and zero errors. The application package includes `Start-PodRelay.cmd`, which detects the x64 .NET 8 Desktop Runtime and, only with user confirmation, downloads the Microsoft installer, validates its Microsoft Authenticode signature, and requests installation. PodRelay itself remains Authenticode-unsigned until the original author supplies a trusted code-signing certificate; GitHub build provenance and SHA-256 provide integrity evidence but do not suppress SmartScreen. The app also completed an isolated real-controller test: while normal automation was disabled and AirPods were on iPhone, reconnecting the bound controller restored the full Windows audio invariant in 4.28 seconds.
