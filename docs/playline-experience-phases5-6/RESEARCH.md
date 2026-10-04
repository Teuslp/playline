# Playline Experience Phases 5–6 Research

## Overview

These phases refine the compact game bar and add persisted daily-use behavior without turning Playline into a full launcher. The implementation must remain event-driven, native to Windows, and idle without polling or continuous animation.

## Problem Statement

The current bar can manage and launch games, but it does not preserve a user-defined order or window position, expose everyday behavior preferences, recover from being hidden, or distinguish favorites and missing executables. The next version needs these conveniences while preserving the existing lightweight architecture.

## User Stories / Use Cases

- Choose icon-only or icon-and-name presentation and apply it immediately.
- Select small, medium, or large game items.
- Mark a game as a favorite without moving it unexpectedly.
- Drag a game to a new position and keep that order after restart.
- Restore the bar on the same visible monitor after restart.
- Start Playline at sign-in without administrator privileges.
- Keep, minimize, hide, or exit Playline after a successful launch.
- Auto-hide on application deactivation and recover from the notification area.
- Identify a missing manual executable while retaining Edit and Remove actions.

## Technical Research

### Approach Options

#### Startup

- Per-user Registry `Run` entry: built into Windows, no elevation, easy to remove.
- Startup-folder shortcut: also valid, but creates and maintains an extra Shell Link.

Use `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` with one quoted executable command. Microsoft documents this key as the per-user mechanism that runs a command at each sign-in.

#### Tray

- Native `Shell_NotifyIcon`: minimum runtime dependency but substantially more interop and menu-lifetime code.
- `System.Windows.Forms.NotifyIcon`: inbox Windows Desktop API, same process, event-driven, and already provides tooltip, menu, and double-click behavior.

Use `NotifyIcon`; enabling Windows Forms in the WPF project adds no third-party package or helper process.

#### Window Placement

- Save every `LocationChanged`: simple but creates unnecessary disk writes.
- Save after `DragMove` completes and when settings change: one write per completed action.

Use the second approach. Validate the saved rectangle against current monitor working areas, clamp partially visible positions, and center on the primary display when no monitor intersects.

#### Auto-hide

- Poll cursor/focus continuously: rejected due to permanent activity.
- Handle WPF activation/deactivation and use one disposable short delay: event-driven and idle-free.

Use `Window.Deactivated` plus a one-shot dispatcher timer. Suppress it while Playline-owned dialogs are open.

#### Reordering

Use WPF pointer/capture events for a horizontal drag gesture and persist explicit `SortOrder` values. No external behavior package is needed.

### Recommended Approach

- Extend `Game` with `IsFavorite` and `SortOrder`.
- Extend `AppSettings` with display mode, item size, after-launch action, auto-hide, position restoration, and nullable coordinates.
- Retain read-only legacy fields long enough to migrate `IconSize` and `CloseAfterGameLaunch`.
- Normalize incomplete and invalid settings after deserialization.
- Keep settings persistence behind `ISettingsRepository`.
- Add pure Core helpers for window-position validation and after-launch decisions so behavior can be tested without real UI.
- Add a small native Windows startup service and a same-process tray service.
- Update `GameItem` with dependency properties for mode/size plus favorite and invalid indicators.

### Required Technologies

- WPF controls, activation events, dispatcher, and drag-and-drop.
- `System.Windows.Forms.NotifyIcon` from the Windows Desktop shared framework.
- `Microsoft.Win32.Registry` for the current-user `Run` value.
- Existing JSON repository and atomic writer.

### Data Requirements

`Game` gains:

- `IsFavorite: bool`
- `SortOrder: int`

`AppSettings` gains:

- `DisplayMode`
- `ItemSize`
- `AlwaysOnTop`
- `StartWithWindows`
- `AfterLaunchAction`
- `AutoHide`
- `RestoreWindowPosition`
- `WindowX`
- `WindowY`

## UI/UX Considerations

- The settings window remains a single compact dialog with Appearance and Behavior groups.
- Favorites receive a small star but remain in the user's explicit order.
- Missing manual executables use reduced opacity and a warning badge; their context menu remains available.
- Name mode truncates long titles instead of growing without bound.
- Feedback uses a temporary non-blocking banner in the bar for operational errors and duplicate notices.
- Confirmation and field-validation dialogs remain modal only where the user must make a decision.

## Integration Points

- `GameLibraryService` owns favorite and ordering mutations.
- `MainWindow` applies settings and coordinates auxiliary windows.
- `JsonSettingsRepository` performs compatibility normalization.
- `App` composes startup, settings, tray, discovery, and launch services.

## Risks and Challenges

- Mixed-DPI monitor coordinates: use current working areas and clamp conservatively.
- Auto-hide while a dialog opens: guard modal operations and cancel pending hide delays.
- Drag gesture accidentally launching a game: suppress the click after a completed drag.
- Registry value drift after the executable moves: update only when the desired command differs.
- Unknown enum values in old/future JSON: deserialize safely and normalize to documented defaults.

## Open Questions

None blocking. Search and global hotkeys remain outside this scope. Favorites will not override manual order.

## References

- [Run and RunOnce Registry Keys](https://learn.microsoft.com/windows/win32/setupapi/run-and-runonce-registry-keys)
- [NotifyIcon component](https://learn.microsoft.com/dotnet/desktop/winforms/controls/notifyicon-component-windows-forms)
- [WPF drag-and-drop overview](https://learn.microsoft.com/dotnet/desktop/wpf/advanced/drag-and-drop-overview)
- [WPF Window.Deactivated](https://learn.microsoft.com/dotnet/api/system.windows.window.deactivated)
- [WPF virtual screen metrics](https://learn.microsoft.com/dotnet/api/system.windows.systemparameters.virtualscreenleft)
