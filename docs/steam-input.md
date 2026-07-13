# Steam Big Picture and Steam Input

PodRelay registers `Ctrl+Alt+A` by default. Steam Input can translate a controller chord to that keyboard shortcut without injecting anything into a game.

PodRelay can also bind a controller in Settings. The binding only observes the Windows controller-added event: when that controller comes online, PodRelay checks the AirPods connection and runs `EnsureConnected` if needed. It does not read or reserve any buttons, so the Steam Input shortcut below remains available for an explicit retry.

## Recommended mapping

Steam changes controller labels between releases, but the flow is generally:

1. Open Steam **Settings → Controller**.
2. Edit **Guide Button Chord Layout** (or the equivalent desktop/Big Picture chord layout).
3. Select the `Guide/Xbox/PS + Y` chord.
4. Bind it to the keyboard chord `Ctrl + Alt + A`.
5. Optionally bind `Guide/Xbox/PS + B` to `Escape`; this dismisses or cancels the focused PodRelay popup without installing a controller hook.
6. Apply the layout.
7. With PodRelay running, open Big Picture and press the chord once.

PodRelay’s primary action is idempotent: pressing the chord again while AirPods are connected keeps them connected and selected; it does not disconnect them.

## Feedback

- The large popup shows discovery, connecting, success, and actionable failure states in Steam Big Picture and borderless windows.
- Exclusive-fullscreen games may cover ordinary desktop windows. PodRelay does not inject an overlay because that could conflict with anti-cheat systems.
- Native gamepad vibration is intentionally deferred until it can be added without stealing normal Steam/game input. Steam Input plus the visual popup is the safe first implementation.
- The popup accepts keyboard `Y`/Enter/Space to connect and `B`/Escape/Back to dismiss. Steam Input can emit these keys, so controller navigation remains opt-in and does not globally intercept gamepad input.
