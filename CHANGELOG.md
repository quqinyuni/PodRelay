# Changelog

## 0.1.3 - 2026-07-16

- Support Windows 11's unified Bluetooth render endpoint without relying on localized endpoint names.
- Select high-quality output using the A2DP service class and Windows endpoint form factor, with conservative fallbacks for vendor drivers that omit metadata.
- Save and validate the currently selected headset before a manual connection request instead of silently returning when no target was previously saved.
- Skip stale Core Audio endpoints without hiding valid paired devices.
- Clean publish output and verify that application, Core, diagnostics, and dependency-manifest versions match before release.

## 0.1.2 - 2026-07-13

- Retry transient Windows Bluetooth audio-endpoint enumeration gaps within the existing connection timeout.
- Distinguish “endpoint not exposed yet” from a real rejected reconnect request.

## 0.1.1 - 2026-07-13

- MIT licensing with original-author attribution to quqinyuni.
- Runtime-aware launcher that offers a verified Microsoft .NET 8 Desktop Runtime install when required.
- Windows CI packaging, GitHub build-provenance attestation, and an Authenticode signing hook for a future trusted certificate.

## 0.1.0 - 2026-07-13

First public release.

- Real Windows enumeration and stable binding for paired Bluetooth audio devices.
- Idempotent AirPods reconnect with Stereo endpoint and default-output verification.
- Automatic relay with lock, cooldown, cancellation, and retry protection.
- AirPods Pro 2 wear/case detection and Windows media pause/resume.
- Native controller-arrival relay and Steam Input global-shortcut workflow.
- Original ten-foot connection popup, settings UI, tray status, and AirPods icons.
- Local-only structured diagnostics and export.
