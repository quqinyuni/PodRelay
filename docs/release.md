# Local release package

The current deliverables are framework-dependent Windows x64 packages. They require the .NET 8 Desktop Runtime and are not code-signed.

| Artifact | SHA-256 |
|---|---|
| `artifacts\PodRelay-win-x64.zip` | `9A3423BBA39E087C78F43AF1F5866ADDADEDDD54B8BB86CF3DF9E49BEA160D25` |
| `artifacts\PodRelay-Diagnostics-win-x64.zip` | `9A0A2507E0B304BF940A0F6C5DAB9450FB18487ADE3CC68E8CD155CA8ED3CF44` |

These SHA-256 hashes were rechecked after the 0.1.0 Release publish on 2026-07-13. The build passed 53 unit tests with zero warnings and zero errors. The package includes the original application/window/tray artwork and opt-in in-ear Windows media pause/resume. The app also completed an isolated real-controller test: while normal automation was disabled and AirPods were on iPhone, reconnecting the bound controller restored the full Windows audio invariant in 4.28 seconds.
