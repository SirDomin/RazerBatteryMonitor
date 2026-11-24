# Razer Battery Monitor

Small, read-only Windows tray monitor made for:

- Razer Naga V2 Pro through Mouse Dock Pro (`1532:00A4`) or its stock receiver;
- Razer DeathAdder V3 Pro (`1532:00B6/00B7` and `1532:00C2/00C3` variants);
- Razer Kraken V3 Pro through the local Razer Synapse status log.
- SteelSeries Arctis Nova 7/7P/7X Gen 2 family through its read-only HID
  status report (`1038:227E` and related transmitters); SteelSeries GG is not
  required.

The application places compact mouse and headset indicators directly beside
the notification-area chevron. Each indicator contains a readable percentage;
a lightning mark means charging. An indicator disappears completely while its
device is offline. Click an indicator to open the complete device list.

Run with `--show` to open the device list immediately; without it the app
starts quietly in the notification area.

## Safety model

- No administrator rights. The manifest explicitly requests `asInvoker`.
- No network, telemetry, updater, installer, service, or driver.
- It only reads the chevron position; it does not modify taskbar settings.
- Mouse communication is restricted to known Razer VID/PID pairs.
- Only Razer getter commands `07/80` (battery) and `07/84` (charging) are sent.
- Kraken V3 Pro support only reads Synapse's local log. No experimental headset
  commands are sent.
- SteelSeries support sends only the documented `00 B0` status query and never
  changes headset settings or firmware.
- No external NuGet packages. The program builds with Windows' built-in .NET
  Framework compiler.

## Build and test

Open PowerShell in the repository and run:

```powershell
.\build.ps1 -RunTests
```

Output: `artifacts\RazerBatteryMonitor.exe`.

One-shot diagnostics without opening the tray UI:

```powershell
.\artifacts\RazerBatteryMonitor.exe --probe-once .\artifacts\probe.txt
```

## Notes

Windows exposes tray entries as square icons rather than free-form text. The
program therefore draws the device glyph, percentage and charging mark into
each icon, and provides the full text in the tooltip and details window.

Razer Synapse can hold a mouse control interface. When a recent Synapse battery
event is available, the program uses that read-only source first. Direct HID is
used for supported mice when no fresh local Synapse value is available.

For Kraken V3 Pro, the last saved battery percentage remains visible while
Windows still detects the headset USB device (`1532:052C`). A stale reading is
shown without a charging mark, because an old log cannot confirm the current
charging state. The indicator disappears after the USB device is disconnected.

