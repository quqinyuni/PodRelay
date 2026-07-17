# Known limitations

- Apple proximity advertisements do not provide a stable public identity that Windows can directly map to one paired AirPods set. PodRelay binds the public model code to the exact Apple product variant reported by the selected Windows Bluetooth device and verifies the paired Windows target, but another nearby AirPods of that exact product variant can still be the trigger. Drivers that omit Bluetooth product metadata fall back to family-level filtering.
- Wear/case detection uses reverse-engineered public Proximity Pairing status bits. It is fail-closed for malformed/unknown frames and is verified against AirPods Pro 2 in this project, but Apple does not document this format and a future firmware can change it.
- Battery parsing is not enabled yet. It is optional by design and cannot block connection or popup behavior.
- `KSPROPERTY_ONESHOT_RECONNECT` and `IPolicyConfig.SetDefaultEndpoint` are Windows system contracts used by existing tools but are not documented as public application APIs. They are isolated behind Windows-specific adapters and verified after every call.
- The tray “release to another device” action intentionally pauses reacquisition rather than forcing a Windows disconnect. The user still selects AirPods from iPhone, iPad, or the other device; that device performs the transfer while PodRelay honors the cooldown.
- PodRelay can natively observe and bind a Windows `RawGameController` without intercepting its buttons. Steam Input remains the way to invoke the shortcut while a controller is already connected; native vibration is not enabled.
- No overlay is injected into exclusive-fullscreen games.
- A Bluetooth device can be “present” with RSSI `-128`, which means Windows has no useful live signal-strength sample. Presence and proximity remain heuristics.
- Classic Bluetooth cannot carry A2DP high-quality stereo and the HFP microphone simultaneously. Automatic call audio therefore keeps HFP for the complete microphone-capture session and restores high quality only after the application releases it; switching on every silence would disconnect the application's microphone stream.
- Core Audio session enumeration covers ordinary shared-mode capture used by mainstream meeting, recording, and game-chat applications. An application using exclusive-mode capture or a manually selected non-AirPods microphone is outside this detector.
- The local development package is not code-signed. Windows SmartScreen or company application-control policy may require the binary to be approved by the device administrator before first launch.

## Candidate next version work

- Add optional, non-exclusive GameInput/XInput vibration feedback only after verifying it does not consume controller input intended for Steam or games.
- Investigate signed, documented alternatives if Microsoft publishes a stable public A2DP connect API; keep the current one-shot adapter isolated until then.
- Add reliable battery display only if frame identity and charge values can be validated across AirPods generations and firmware revisions.
- Add an opt-in audio render probe if ACTIVE plus default-route verification proves insufficient on a specific driver, without emitting an unwanted audible sound.
