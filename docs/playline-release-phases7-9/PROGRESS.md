# Playline Release Phases 7–9 Progress

## Status: Completed

## Quick Reference

- Research: `docs/playline-release-phases7-9/RESEARCH.md`
- Implementation: `docs/playline-release-phases7-9/IMPLEMENTATION.md`
- Release notes: `docs/playline-release-phases7-9/RELEASE_NOTES.md`

## Phase Progress

### Phase 1: Baseline Profiling and Audit

**Status:** Completed

- Audited dependencies, timers, polling, persistence, scanners, image loading and launch construction.
- Confirmed the baseline had no third-party production packages, watcher, polling loop or permanent worker.
- Compared framework-dependent, self-contained, ReadyToRun and single-file candidates.
- Measured 0, 1, 10, 50, 100 and 250 simulated games.

### Phase 2: Targeted Optimization and Robustness

**Status:** Completed

- Replaced quadratic deduplication with indexed identities.
- Reduced mean load time for 250 games from 61.2 ms to 5.4 ms.
- Added corrupt-JSON quarantine and bounded critical-log rotation.
- Hardened Steam manifests, shortcuts, launch URIs and file failure paths.
- Expanded the suite from 46 to 61 passing tests.

### Phase 3: Release Metadata and Publishing

**Status:** Completed

- Added metadata, version `0.9.0-beta`, x64 target, placeholder icon and Per-Monitor V2 configuration.
- Selected self-contained folder publishing without trimming, single-file or ReadyToRun.
- Restricted satellite resources and removed PDBs from distribution.
- Added repeatable build, measurement and validation scripts.

### Phase 4: Installer and Portable Validation

**Status:** Completed

- Compiled a current-user Inno Setup installer and portable ZIP.
- Generated and independently recalculated SHA-256 checksums.
- Passed first install, first launch, in-place upgrade, launch after upgrade, uninstall and portable launch.
- Confirmed removal of program files, preservation of user data and zero residual started processes.

### Phase 5: Final Visual and Documentation Pass

**Status:** Completed

- Added images to the discovery list and automatic artwork repair for existing libraries.
- Added native Windows selectors for executable, directory and custom image.
- Redesigned the bar with a neutral/cyan palette, 72 px medium height and adaptive width.
- Visually verified empty and populated states; three detected games rendered three images.
- Updated README and release notes.

### Phase 6: Glass Surface and Vertical Layout

**Status:** Completed

- Replaced the opaque gray surface with a lightweight navy glass composition built from WPF primitives.
- Matched the selected visual reference with a bright ice border, warm orange/red upper-left glow and cool lower-right reflection.
- Added a complete dark `ComboBox` template so selected values and dropdown items remain legible.
- Added a persisted horizontal/vertical orientation preference.
- Adapted sizing, scrolling, keyboard navigation, drag reordering and directional menu labels to both orientations.
- Visually verified a 232 × 72 horizontal bar, a 74 × 232 vertical bar and the settings dropdown popup.
- Kept the production dependency graph unchanged and added no timers or background work.

### Phase 7: Segmented Glass Dock

**Status:** Completed

- Rebuilt the horizontal bar from the supplied dock reference instead of only recoloring the prior layout.
- Increased medium height to 90 px and the adaptive horizontal range to 260–760 px.
- Added larger rounded cover cards, long separators and a three-circle overflow affordance.
- Shifted the glass material to deep blue with lilac and ice-blue reflections and a glossy top sheen.
- Added orientation-specific item metrics so vertical mode keeps balanced proportions and horizontal separators.
- Visually verified two real game images in a 332 × 90 horizontal bar and name mode in a 174 × 238 vertical bar.

### Phase 8: Native Acrylic Composition

**Status:** Completed

- Replaced the simulated opaque glass base with DWM-backed Acrylic through `FluentWpfCore` 1.0.6.
- Kept the custom gradients only as low-opacity tint, sheen and edge reflections so the desktop remains visible through the material.
- Switched the main HWND away from WPF layered-window transparency and retained custom chrome, drag, tray behavior and rounded surface clipping.
- Reduced game-card corner radii to match the supplied dock reference more closely.
- Increased medium vertical covers from 40 px to 60 px and the vertical bar from 80 px to 96 px for parity with horizontal mode.
- Visually verified both orientations over live desktop and video backgrounds on Windows 10 22H2.
- Added the MIT third-party notice to build and publish outputs.

## Final Metrics

| Metric | Result |
|---|---:|
| Tests | 61/61 passed |
| Build warnings/errors | 0/0 |
| Typical 10-game startup | 1.17 s mean |
| 250-game startup | 2.39 s mean |
| Idle CPU after settling, 10 s | 0 ms |
| Idle writes, 10 s | 0 bytes |
| Published files | 272 |
| Published size | 155.67 MB |
| PDB files in payload | 0 |
| Installer size | 51,865,081 bytes |
| Portable ZIP size | 68,136,476 bytes |

## Final Artifacts

- `artifacts/Playline-Setup-x64.exe`
- `artifacts/Playline-Portable-x64.zip`
- `artifacts/checksums.txt`
- `artifacts/release-notes.md`

## Architectural Decisions

- Preserve event-driven behavior and avoid speculative background work.
- Install per user and preserve `%LOCALAPPDATA%\Playline` on uninstall.
- Prefer a self-contained folder payload because measured single-file/ReadyToRun candidates did not improve startup meaningfully.
- Keep controls and layout on WPF primitives; use one focused 54 KB composition dependency instead of adopting a full UI framework.
- Use local executable icons and Steam artwork cache rather than network image downloads.
- Keep the generated application icon as an explicitly documented beta placeholder.

## Known Limitations

- Unsigned binaries may trigger a Windows reputation warning.
- The placeholder app icon should be replaced before 1.0.
- Physical mixed-DPI coverage is limited to the available validation hardware.
- The current release is Windows x64 only and automatically detects Steam/Epic only.
