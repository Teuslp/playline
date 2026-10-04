# Playline Functional Phases 3–4 Implementation Plan

## Phase 3: Manual Management and Launching

- [x] Extend `Game` and implement normalized stable identities.
- [x] Add update and path-aware deduplication to `GameLibraryService`.
- [x] Resolve `.exe` and `.lnk` selections.
- [x] Extract and cache local icons.
- [x] Launch executables and launcher URIs safely.
- [x] Add manual selection, editing, removal, context menu, and immediate UI refresh.

## Phase 4: Initial Automatic Discovery

- [x] Add the scanner contract and resilient aggregation service.
- [x] Locate and scan every Steam library.
- [x] Parse Steam app manifests and produce stable games.
- [x] Locate and parse installed Epic manifests.
- [x] Add a compact selection window for scan results.
- [x] Add selected games through `GameLibraryService` without duplicates.

## Validation

- [x] Build with no warnings or errors.
- [x] Pass unit/integration tests with simulated Steam/Epic installations.
- [x] Validate file-dialog cancellation and manual `.exe`/`.lnk` flows.
- [x] Validate edit, removal, restart persistence, and duplicate rejection.
- [x] Confirm scanning is an explicit, cancellable operation with no background worker.
