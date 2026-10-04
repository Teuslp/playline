# Playline Functional Phases 3–4 Progress

## Status: Complete

## Delivered

- Stable string identities, path normalization, update support, and deduplication.
- Native `.lnk` resolution with target, arguments, and working directory.
- Friendly executable names, 64 px PNG icon cache, and safe game launching.
- Manual add, edit, remove, context menu, and immediate UI refresh.
- On-demand Steam and Epic scanners behind `IGameScanner`.
- Steam registry/default location lookup, all configured libraries, and ACF parsing.
- Epic manifest location/parsing with launcher URI or executable metadata.
- Compact discovery window with selection and existing-game indication.
- Simulated scanner fixtures and end-to-end persistence tests.

## Validation

- Release build: 0 warnings, 0 errors.
- Automated tests: 29 passed, 0 failed, 0 skipped.
- Formatting: `dotnet format --verify-no-changes` passed.
- Real isolated UI flow passed for file-picker cancellation, `.exe` add, duplicate rejection, edit, restart persistence, `.lnk` resolution, icon generation, launch with arguments, removal, empty state, and discovery-window cancellation.
- The combined simulated Steam + Epic scan passed without relying on installed launchers.

## Decisions

- No production NuGet dependency was added.
- Shell Links, executable icons, and process launch use Windows/.NET APIs.
- The scan runs only after **Procurar jogos** and owns no timer, watcher, scheduler, or persistent task.
- Development data-directory overrides remain Debug-only.

## Problems Found and Resolved

- Corrected the Unicode entry point used for `SHGetFileInfoW`.
- Explicitly initialized COM on the icon-extraction thread and always released native icon handles.
- Filtered malformed manifest data and invalid Registry paths without aborting the application.
- Adjusted UI validation to target the editable child of the native Windows file-name combo box.

## Blockers

- None.

## Session Log

### 2026-10-03

- Completed the combined Phase 3 and Phase 4 implementation and validation.
