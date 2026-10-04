# Playline Experience Phases 5–6 Implementation Plan

## Overview

Implement persisted presentation and behavior settings, user-controlled game order, favorites, monitor-safe placement, per-user startup, auto-hide, and tray recovery using only Windows Desktop APIs already available with .NET.

## Prerequisites

- Phases 1–4 complete and passing.
- Existing JSON repositories and `GameLibraryService` remain the only persistence route.
- Steam and Epic scan behavior remains unchanged and explicitly invoked.

## Phase Summary

1. Domain and compatible persistence.
2. Windows behavior services.
3. Settings and refined game-item UI.
4. Reordering, tray, auto-hide, and feedback.
5. Automated and real-environment validation.

---

## Phase 1: Domain and Persistence

### Objective

Represent favorites, order, display preferences, placement, and post-launch behavior safely.

### Tasks

- [x] Extend `Game` with favorite and explicit order.
- [x] Add favorite and reorder operations to `GameLibraryService`.
- [x] Extend and normalize `AppSettings`.
- [x] Migrate legacy icon-size and close-after-launch properties.
- [x] Tolerate missing and unknown settings values.

### Success Criteria

Old files load, defaults remain sensible, duplicates remain rejected, and game order survives reload.

### Files Likely Affected

- `Playline.Core/Models`
- `Playline.Core/Services/GameLibraryService.cs`
- `Playline.Storage/Settings`

---

## Phase 2: Native Windows Behavior

### Objective

Add startup registration and monitor-safe placement without elevation or background services.

### Tasks

- [x] Implement current-user startup registration.
- [x] Implement pure placement validation and monitor adapters.
- [x] Persist position only after completed movement.
- [x] Implement post-launch action mapping.

### Success Criteria

Startup toggles cleanly, positions restore or recover to a visible screen, and all four post-launch actions work only after successful launches.

### Files Likely Affected

- `Playline.Windows/Startup`
- `Playline.Core/Services`
- `Playline.App/MainWindow.xaml.cs`

---

## Phase 3: Settings and Visual Modes

### Objective

Expose a compact settings dialog and update the existing bar without restart.

### Tasks

- [x] Add the settings window.
- [x] Implement compact and name display modes.
- [x] Implement small, medium, and large item sizes.
- [x] Add favorite and invalid-game indicators.
- [x] Preserve efficient image loading.

### Success Criteria

Every appearance setting updates immediately and persists across restart.

### Files Likely Affected

- `Playline.App/SettingsWindow.xaml(.cs)`
- `Playline.App/Controls/GameItem.xaml(.cs)`
- `Playline.App/Resources`

---

## Phase 4: Interaction, Tray, and Auto-hide

### Objective

Complete daily-use interaction while keeping the application event-driven.

### Tasks

- [x] Add favorite context action.
- [x] Add WPF pointer-driven drag ordering.
- [x] Add the same-process notification icon and menu.
- [x] Implement event-driven auto-hide with a disposable delay.
- [x] Add non-blocking status feedback and keyboard navigation.

### Success Criteria

The bar can hide and recover, Exit terminates the process, and no permanent timer or scanner remains active.

### Files Likely Affected

- `Playline.App/TrayIconService.cs`
- `Playline.App/MainWindow.xaml(.cs)`
- `Playline.App/Controls`

---

## Phase 5: Verification and Documentation

### Objective

Prove compatibility, behavior, resource use, and completeness.

### Tasks

- [x] Add domain, repository, startup, and policy tests.
- [x] Run Debug and Release build/test/format.
- [x] Validate settings and interaction through real UI automation.
- [x] Validate startup activation/deactivation with Registry restoration.
- [x] Sample idle CPU and working set.
- [x] Update README and progress documentation.

### Success Criteria

All automated tests pass, the mandatory UI scenarios pass in isolated data, Registry state is restored, idle CPU is approximately zero, and no Playline process is left running.

---

## Post-Implementation

- [x] Documentation updates
- [x] Validation summary
- [x] Confirm no new launcher or packaging scope was introduced

## Notes

- Search is intentionally deferred because it is optional and would compete for the compact layout.
- The tray icon is always available so Hide, Minimize, and auto-hide remain recoverable.
- Favorites do not silently change explicit drag order.
