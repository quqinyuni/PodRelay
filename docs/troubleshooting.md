# Troubleshooting

## The target is not listed

Pair AirPods once in Windows Bluetooth Settings, then select **Refresh paired devices**. PodRelay does not initiate first-time pairing and never removes a pairing.

If an older build reports `0x80070002` while reading devices, update PodRelay and refresh again. This error can occur when Windows keeps a cached audio endpoint whose backing device has already disappeared. Current builds skip only that stale endpoint and continue listing the remaining paired Bluetooth headphones; they do not delete or repair Windows device records.

## “AirPods not found”

Open the case or wear the earbuds. A closed sleeping case may not be connectable. If AirPods are in an active phone call, the other device may retain the audio profile.

## Bluetooth connected but audio not ready

PodRelay requires the high-quality endpoint whose name contains `Stereo`. It deliberately does not accept Hands-Free AG Audio as success. Wait a few seconds and retry; export diagnostics if the Stereo endpoint remains Unplugged/NotPresent.

## Shortcut registration failed

Another application already owns the selected shortcut or the text could not be parsed. Supported modifier names are `Control`, `Alt`, `Shift`, and `Windows`, separated by commas. The key is a WPF key name such as `A`, `F9`, or `MediaPlayPause`.

## The popup does not appear over a game

Ordinary and borderless windows are supported. Exclusive-fullscreen games can cover the popup. Use the Steam Input shortcut; PodRelay intentionally avoids process injection.

## Export diagnostics

Open settings and choose **Export diagnostics**. The ZIP contains only `%LOCALAPPDATA%\PodRelay`, including settings and local JSONL logs. Review or redact the Bluetooth address before sharing it.
