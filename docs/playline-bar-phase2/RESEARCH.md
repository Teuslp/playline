# Playline Bar — Phase 2 Research

## Overview

Phase 2 replaces the diagnostic window with a compact game dock while preserving the Phase 1 service and storage boundaries.

## Recommended Approach

- Use a borderless WPF `Window` with a transparent outer surface and one rounded, mostly opaque `Border`.
- Keep the fixed window at 580 × 88 device-independent pixels and center it on startup.
- Bind the actual `Game` instances supplied by `GameLibraryService` to an `ItemsControl`.
- Represent each game with a reusable `GameItem` user control.
- Place the horizontal items panel inside a `ScrollViewer` and translate mouse-wheel input into horizontal offsets.
- Use a native WPF `ContextMenu` for the overflow menu.
- Keep window drag, keyboard shortcuts, menu opening, and window closing in code-behind.
- Offer debug-only `--demo-count=N` data for visual validation without writing mocks to `games.json`.

## UI and Performance Considerations

- No animations, blur, shadows, timers, background services, or image downloads.
- Hidden scrollbars keep the dock compact; mouse wheel and keyboard focus remain available.
- The transparent surface is limited to a very small, fixed-size window.
- Colors and common control styles live in two resource dictionaries.
- Production builds show only the real library or the empty state.

## Integration Points

- `App` continues to initialize settings and `GameLibraryService`.
- `MainWindow` receives an immutable game list and never reads JSON.
- `GameItem` receives the existing `Game` model through a dependency property.

## Risks and Mitigations

- Drag starting over controls: ignore routed clicks whose visual ancestors contain a button.
- Large libraries: clip content to a fixed viewport and scroll horizontally.
- Mock data leaking into persistence: compile the demo-data path only in Debug builds.
- Popup remaining open: rely on native menu behavior and explicitly handle Escape.

## References

- [Windows in WPF overview](https://learn.microsoft.com/dotnet/desktop/wpf/windows/)
- [Window.DragMove](https://learn.microsoft.com/dotnet/api/system.windows.window.dragmove)
- [ScrollViewer](https://learn.microsoft.com/dotnet/api/system.windows.controls.scrollviewer)
- [MenuItem](https://learn.microsoft.com/dotnet/desktop/wpf/controls/menuitem)

