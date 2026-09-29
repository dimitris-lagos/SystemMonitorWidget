SystemMonitorWidget v2.6.5 removes the startup fan surge and completes the transition to the embedded single-EXE runtime.

### Smooth fan-control startup
- Leaves every fan output untouched while the widget waits for the first valid temperature reading for every enabled curve.
- Does not launch the elevated fan helper or write any percentage during that waiting period.
- Applies the configured curves together as soon as all required temperatures are valid.
- Keeps the configured missing-sensor fail-safe active after fan control has started normally.

### Lean release package
- Ships only the main widget EXE and the OpenHardwareMonitor license in the release ZIP.
- Continues to extract and verify the embedded fan helper, HWiNFO restart helper, and OpenHardwareMonitor runtime before use.
- Removes the completed one-release `%LOCALAPPDATA%\VegaDesktopWidget` migration path. Settings remain in `%LOCALAPPDATA%\SystemMonitorWidget`.

### Validation
- Passed the startup fan-hold test without opening the helper or changing hardware controls before temperatures are ready.
- Passed the stale-temperature fail-safe, independent-worker, sensor snapshot, embedded-runtime, update replacement/rollback, tray-menu, and network-formatting tests.
