# Playline Experience Phases 5–6 Progress

## Status: Complete

## Quick Reference

- Research: `docs/playline-experience-phases5-6/RESEARCH.md`
- Implementation: `docs/playline-experience-phases5-6/IMPLEMENTATION.md`

## Phase Progress

### Phase 1: Domain and Persistence

**Status:** Complete

- Added safe settings normalization and legacy migration.
- Added persisted favorites and explicit game order.
- Preserved deduplication and normalized order after every mutation.

### Phase 2: Native Windows Behavior

**Status:** Complete

- Added current-user startup registration under `HKCU\...\Run`.
- Added monitor-aware placement validation and post-launch policy.
- Position is written only after a completed drag or a settings correction.

### Phase 3: Settings and Visual Modes

**Status:** Complete

- Added compact settings UI with immediate application.
- Added compact/name modes and small/medium/large sizes.
- Added favorite and missing-executable indicators.
- Icons use decode sizing and `BitmapCacheOption.OnLoad`.

### Phase 4: Interaction, Tray, and Auto-hide

**Status:** Complete

- Added pointer-driven drag reorder plus context-menu move fallback.
- Added same-process tray icon and menu.
- Added event-driven auto-hide with a short disposable timer.
- Added non-blocking status feedback and keyboard navigation.

### Phase 5: Verification and Documentation

**Status:** Complete

- Release build, tests, formatter, real UI flows, Registry toggle, and idle sampling passed.
- README and feature documentation updated.

## Validation Results

| Check | Result |
|---|---|
| Release rebuild | Passed, 0 warnings and 0 errors |
| Automated tests | 47/47 passed |
| `dotnet format --verify-no-changes` | Passed |
| Compact / small | 580×76 window, 42×42 clickable item |
| Name / medium | 580×88 window, 148×54 clickable item |
| Name / large | 580×100 window, 174 px clickable item width |
| Favorite | Persisted without changing manual order |
| Drag reorder | UI and JSON changed; order survived restart |
| Window position | Dragged position saved and restored |
| Invalid position | `4000,3000` corrected and persisted as visible `670,481` |
| Always on top | Native `WS_EX_TOPMOST` state observed |
| Start with Windows | HKCU value added with quoted executable and removed again |
| Post-launch actions | Keep, minimize, hide, and exit passed after a real process launch |
| Auto-hide | Deactivation hid the window; process remained alive |
| Tray | Left click restored the hidden bar; tray **Sair** ended the process |
| Keyboard | Right arrow, Enter, Esc, and Ctrl+Q passed |
| Missing executable | Warning exposed; Edit and Remove remained enabled |
| Idle CPU | 0 ms process CPU over a 10 s Release sample |
| Memory, two games | ~106 MB private / ~164 MB working set |
| Memory, empty library | ~92 MB private / ~148 MB working set |
| Cleanup | No Playline process or test startup Registry value remained |

Resource figures are point-in-time measurements on the validation machine and include the WPF/.NET runtime working set.

## Files Created

- `src/Playline.App/SettingsWindow.xaml(.cs)`
- `src/Playline.App/TrayIconService.cs`
- `src/Playline.App/Controls/GameReorderEventArgs.cs`
- `src/Playline.Core/Models/{AfterLaunchAction,DisplayArea,GameDisplayMode,GameItemSize,WindowPosition}.cs`
- `src/Playline.Core/Services/{AfterLaunchPolicy,WindowPositionService}.cs`
- `src/Playline.Storage/Json/SafeEnumConverterFactory.cs`
- `src/Playline.Windows/Startup/{IStartupRegistrationService,StartupRegistrationService}.cs`
- Phase 5–6 research, implementation, and progress documents.
- Tests for launch policy, window placement, startup, favorites, ordering, migration, and persistence.

## Main Files Modified

- `README.md`
- `src/Playline.App/{App.xaml.cs,MainWindow.xaml,MainWindow.xaml.cs,Playline.App.csproj}`
- `src/Playline.App/Controls/GameItem.xaml(.cs)`
- `src/Playline.Core/Models/{AppSettings,Game}.cs`
- `src/Playline.Core/Services/GameLibraryService.cs`
- `src/Playline.Storage/Json/JsonDefaults.cs`
- `src/Playline.Storage/Settings/JsonSettingsRepository.cs`
- Existing library and settings tests.

## Architectural Decisions

- Favorites are metadata and never silently override explicit user order.
- Startup uses the per-user Registry and never requests elevation.
- Tray uses the Windows Desktop inbox `NotifyIcon`; no package or helper process was added.
- Auto-hide is driven by activation events; its delay timer exists only during a pending hide.
- Search remains deferred because it was optional and would add controls to the compact bar.

## Current Limitations

- Per-monitor placement is conservative; unusual mixed-DPI arrangements can be clamped more than expected because WPF and WinForms expose different coordinate spaces.
- The tray icon uses the executable-associated icon until a dedicated product icon is supplied.
- Search, global hotkeys, gamepad support, packaging, and new launchers remain intentionally outside this phase.
