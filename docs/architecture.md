# PodRelay architecture

PodRelay is a Windows-native AirPods handoff utility. Reliability of the Bluetooth audio connection is the primary requirement; presentation features consume the connection state but never define it.

## Platform choice

The implementation uses C# and .NET 8 on Windows 10 build 19041 or newer. WPF is the intended desktop shell because the product needs a lightweight notification-area process, a focusable ten-foot popup, per-monitor DPI support, and direct Win32 interop without requiring the Windows App SDK runtime. The feasibility tools do not depend on WPF.

## Layers

1. **Discovery** enumerates paired Bluetooth association endpoints, decodes the public model/status portion of Apple Proximity Pairing advertisements, and observes Windows raw game-controller arrival events. Advertisement state changes bypass duplicate suppression so a quick case-to-ear transition is not lost.
2. **Connection adapters** use a layered path: reuse a verified existing connection; otherwise request one-shot Bluetooth-audio reconnect; wait for Stereo ACTIVE; select all three default-output roles; finally re-observe the complete success invariant. Each layer produces a structured `ConnectionAttempt`. Experimental or version-sensitive mechanisms remain isolated behind an adapter interface.
3. **Audio** enumerates Core Audio endpoints, verifies that the selected stereo endpoint is active, applies output routing, and sends distinct Windows `APPCOMMAND_MEDIA_PAUSE` / `APPCOMMAND_MEDIA_PLAY` commands for opt-in in-ear control.
4. **Orchestration** owns the idempotent `EnsureConnected` operation, cancellation, single-flight concurrency, backoff, cooldown, and session-lock policy.
5. **Presentation** contains the tray UI, settings, global hotkey, and Apple TV-inspired popup.

The wear-state decoder is based on the public packet layout and UTP in-ear/in-case states documented by the reverse-engineering study [Discontinued Privacy: Leaks in Apple BLE Continuity Protocols](https://petsymposium.org/2020/files/papers/issue1/popets-2020-0003.pdf). Controller binding uses Microsoft's documented [`RawGameController.NonRoamableId`](https://learn.microsoft.com/windows/uwp/gaming/raw-game-controller) and controller-added event; PodRelay does not poll or consume button state.

The two public in-ear bits are retained independently. A 180 ms stability delay filters rapid status chatter while keeping the response close to the AirPods advertisement cadence. A decrease in the worn-earbud count sends the non-toggling pause command only when the target AirPods connection was verified recently. Reinsertion sends the distinct play command only to the same still-valid window that received PodRelay's pause. After a successful resume, a three-second settling window ignores a partial one-pod sensor fallback but never ignores a transition to zero worn pods. This avoids both post-insertion pause bounce and the dangerous `PLAY_PAUSE` toggle, which could start an originally paused video when an earbud is removed. The packaged-media-session API was intentionally rejected because it returns access denied for this unpackaged desktop deployment.

The connection coordinator is single-flight within a process, and the desktop shell additionally holds a per-user named mutex. Together they prevent repeated clicks, hotkeys, startup races, or duplicate processes from issuing competing reconnect operations.

## Connection success invariant

An operation is successful only when all of the following are true:

- the selected Bluetooth device reports connected;
- its stereo render endpoint is active;
- that endpoint is the intended Windows output;
- optional endpoint activation succeeds when a stronger verification is requested.

Hands-Free AG Audio is never accepted as the high-quality render endpoint.

## Safety boundaries

- No pairing removal, driver installation, Bluetooth service disable/enable, or process injection without an explicit separate user-approved experiment.
- No Apple ID, iCloud, or MagicPairing emulation.
- No telemetry; diagnostics are local and redactable.
- A user cancellation or explicit disconnect creates a cooldown and suppresses automatic reacquisition.

## Desktop lifecycle

When configured for Windows startup, the per-user Run entry starts PodRelay with `--background`. Application startup and unlock are evaluated through the same automatic-relay policy, but a disconnected target is deferred until there is a fresh in-ear frame, the bound controller appears, or the user requests a manual connection. An already-connected target is still checked idempotently. Disabled automation, lock state, cooldown, and retry backoff remain authoritative. WPF is declared Per-Monitor V2 DPI-aware; the popup uses the nearest monitor's work area rather than assuming the primary display.
