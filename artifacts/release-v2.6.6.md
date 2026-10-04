## Fan startup reliability

- Applies configured fan curves as soon as the first complete HWiNFO temperature snapshot is available.
- Keeps the last valid temperature readings through brief empty or partial HWiNFO snapshots during startup, preventing a transient sensor gap from sending fans to the 100% fail-safe.
- Requires ten continuous seconds of valid readings before enabling the normal stale-reading fail-safe, with a bounded 90-second startup grace period.
- Preserves the existing live sensor-loss protection after startup has stabilized.

The startup path was verified against the real fan controller: the helper connected in 0.70 seconds, CPU stayed at 1376–1400 RPM, Pump at 2755–2783 RPM, and System 4 at 1267–1374 RPM. The full automated test suite also passes, including first-sample activation, transient HWiNFO gaps, startup grace, stability transition, and live stale-sensor fail-safe coverage.
