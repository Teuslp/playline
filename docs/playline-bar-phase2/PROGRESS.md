# Playline Bar — Phase 2 Progress

## Status: Phase 2 - Completed

## Quick Reference

- Research: `docs/playline-bar-phase2/RESEARCH.md`
- Implementation: `docs/playline-bar-phase2/IMPLEMENTATION.md`

## Tasks Completed

- Phase 1 code and architecture reviewed.
- WPF window, drag, scroll, and menu APIs researched.
- Phase 2 scope and implementation plan recorded.
- Dark colors and shared control styles created.
- Reusable `GameItem` connected directly to the `Game` model.
- Compact horizontal window, empty state, scrolling, dragging, and menu implemented.
- Debug-only visual data implemented without touching persistence.
- Release build and all automated and UI validations completed.

## Decisions Made

- Use WPF transparency only for the small rounded window surface.
- Use code-behind only for window-specific interaction.
- Use real `Game` models directly in `GameItem`.
- Keep mock data Debug-only and opt-in by command-line argument.
- Keep the fixed viewport simple instead of introducing a virtualized selector for only 100 lightweight placeholders.

## Blockers

- None.

## Session Log

### 2026-10-03

- Started implementation of the first horizontal bar version.
- Validated empty, 1-item, 10-item, and 100-item layouts.
- Validated horizontal wheel scrolling through before/after captures.
- Validated a real 120 × 60 pixel window drag.
- Validated menu opening, Escape dismissal, Ctrl+Q, and the Exit command.
- Release build completed with 0 warnings and 0 errors.
- All 10 automated tests passed.
- Measured a cold startup of about 1.68 seconds, 131.7 MB working set, 88.4 MB private memory, and 0% CPU over a three-second idle sample.
- Confirmed normal exit code 0 and no remaining Playline process.

## Files Created

- `src/Playline.App/Controls/GameItem.xaml`
- `src/Playline.App/Controls/GameItem.xaml.cs`
- `src/Playline.App/Resources/Colors.xaml`
- `src/Playline.App/Resources/Styles.xaml`
- `src/Playline.App/DevelopmentGameData.cs`
- `docs/playline-bar-phase2/*`

## Files Modified

- `src/Playline.App/App.xaml`
- `src/Playline.App/App.xaml.cs`
- `src/Playline.App/MainWindow.xaml`
- `src/Playline.App/MainWindow.xaml.cs`
- `README.md`

## Issues Resolved

- Prevented drag from starting on game and menu buttons by checking routed-event ancestors.
- Kept mock data out of `games.json` by compiling the argument override only in Debug.
- Repeated the scroll screenshot with the test window foregrounded after the first capture was occluded by another desktop window.
