# SystemMonitorWidget

A lightweight, modular Windows desktop hardware monitor powered by HWiNFO shared-memory sensor data. Build separate 3-column and 4-column dashboards, connect any available sensor, and control the presentation of every component.

## Screenshots

| 3-column dashboard | 4-column dashboard |
| --- | --- |
| ![3-column dashboard](docs/images/dashboard-3-column.png) | ![4-column dashboard](docs/images/dashboard-4-column.png) |

![Dashboard editor](docs/images/dashboard-editor.png)

![Search all HWiNFO sensors](docs/images/sensor-search.png)

![Four-point fan-control editor](docs/images/fan-control.png)

## Features

- Fully configurable 3-column and 4-column dashboards.
- Optional top-three process strip: hide it, rank by CPU, or rank by RAM while showing aggregated CPU and memory usage.
- Editable, version-aware header title with compact Windows system uptime.
- The dashboard is click-through except for a compact gear button that opens the control menu.
- The gear menu can lock the widget position to prevent accidental header dragging.
- Search and add every sensor exposed through HWiNFO shared memory by label, device, unit, or sensor type.
- Six reusable component types: Big metric, Horizontal spec, Vertical spec, Graph, Compact graph, and Section.
- Full-width, half-height compact graphs keep the title, current value, and sparkline readable in one dashboard row.
- A default system section adds full-width upload and download graphs with optional recorded minimum and maximum values.
- CPU, GPU, and network section titles initialize once from the matching HWiNFO device names and remain editable afterward.
- Drag-and-drop placement with grid snapping and overlap prevention.
- Precise column, half-row, width, and dashboard-height controls.
- Custom display name for every component, with longer section headings that shrink to fit the available width.
- Per-component value formatting with automatic, 0-, 1-, or 2-decimal precision, unit visibility, a 60–160% value-font control, and a live preview.
- Five colors and four change thresholds, configured independently per component, with the RAM-style alert palette as the default.
- Custom colors are shared across every component and dashboard, persist between sessions, and use the next free palette slot when added.
- Fixed minimum and maximum for every graph.
- Optional recorded minimum and maximum on Big metric components.
- Duplicate, delete, and reset-layout actions.
- Interface scaling at 100%, 95%, 90%, 85%, 80%, 75%, 67%, 50%, 33%, and 25%.
- Always-on-top, opacity, refresh interval, and Start with Windows controls in Appearance & behavior.
- A built-in monitor-and-pulse EXE icon, generated at 16, 24, 32, 48, 64, and 256 pixels.
- Checks GitHub's latest stable release at startup and asks before downloading or installing; **Check for updates…** is also in the gear menu.
- Network speeds above 1023 KB/s are displayed in MB/s, including recorded minimum and maximum values.
- Optional Super I/O fan control with automatic channel detection through the bundled OpenHardwareMonitor library.
- Optional HWiNFO32/64 autorestart after 11 hours and 30 minutes of process uptime, configured in Appearance & behavior.
- Portable and installed HWiNFO32/64 detection beside the widget first, then in the default Program Files locations, with an editable path shared by autostart and autorestart.
- Four-point interactive fan curves bound to any live HWiNFO temperature sensor.
- Each control can be paired with a live Super I/O RPM sensor; matching control/fan indices are paired automatically after scanning.
- Per-channel minimum output and missing-sensor fail-safe output.
- Fan writes run in a separate administrator helper; closing the widget restores each controlled channel to firmware/default mode.
- Apply validates and saves changes without closing Configure; OK applies and closes.
- Existing dashboard settings are migrated automatically for the new system section, compact network graphs, colors, and hardware-derived section titles.

## Requirements

- Windows Vista, 7, 8, 8.1, 10, or 11, 64-bit.
- HWiNFO32 or HWiNFO64 with Shared Memory Support enabled.
- .NET Framework 4.x.
- Administrator approval when scanning or enabling Super I/O fan control.

HWiNFO is a separate application and is not bundled with this repository or its releases.

## Dashboard workflow

1. Select the gear button in the header, then choose **Configure dashboard**.
2. Choose the 3-column or 4-column dashboard.
3. Select **Add**, choose a sensor, and choose a component type.
4. Drag the component to a free grid position, or enter its exact position in the inspector.
5. Rename it, set graph bounds if applicable, and configure its five-step color scale.
6. Select **OK** to validate, save, and close the editor.

## Fan-control workflow

1. Keep fan control disabled and select **Scan Super I/O**. The read-only scan discovers control channels and fan RPM sensors.
2. Choose a detected channel, bind its RPM reading, select an HWiNFO temperature source, and edit the four points by dragging the graph or entering exact values.
3. Set a safe minimum and fail-safe output, then enable the channel.
4. Close Open Hardware Monitor before enabling control, because two programs must not write the same controller.
5. Select **Enable fan control after Apply / OK**. Use **Apply** to keep Configure open, or **OK** to apply and close.

## Updates

When a newer stable release is available, choose **Yes** to download the complete ZIP and verify its published SHA-256 checksum. The widget closes normally (returning fan channels to firmware control), then a temporary updater replaces the four application files in the same folder and restarts it. The running EXE keeps its filename, but its internal version changes. Settings in local application data are not replaced.

If the installation folder is protected, Windows may ask for administrator approval. If an application file remains locked, the update stops and restores previous files where possible. Network failures are silent during startup; use **Check for updates…** to see the error.

Windows Vista does not support the TLS 1.2 connection needed for GitHub release downloads, so its built-in update check is unavailable. Download the latest ZIP manually on another supported system and copy the four files over after closing the widget.


## Build from source

Run PowerShell from the repository root:

```powershell
.\build.ps1
```

The build writes the individual binaries plus `artifacts\SystemMonitorWidget-v2.6.2-win-x64.zip` and its SHA-256 checksum.

## Local data

Widget settings stay in the current Windows user's local application-data folder. The repository contains no exported sensor logs, local settings, or user-specific paths.

## Quick start

1. Download `SystemMonitorWidget-v2.6.2-win-x64.zip` from the [latest release](../../releases/latest).
2. Extract all four files into the same folder.
3. Open HWiNFO32/64 settings and enable **Shared Memory Support**.
4. Start HWiNFO sensors.
5. Run `SystemMonitorWidget-v2.6.2.exe`.
6. Select the header gear and choose **Configure dashboard** to edit the dashboard or configure fan curves.
