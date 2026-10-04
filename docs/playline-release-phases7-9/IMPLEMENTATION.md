# Playline Release Phases 7–9 Implementation Plan

## Overview

Measure the current Release build, make only evidence-backed reliability/performance changes, then generate and validate a Windows x64 beta installer and portable archive.

## Prerequisites

- Phases 1–6 complete and passing.
- .NET 10 SDK available.
- Windows x64 validation host.
- Official Inno Setup compiler acquired for installer generation.

## Phase Summary

1. Baseline profiling and audit.
2. Targeted optimization and robustness.
3. Release metadata and publishing.
4. Installer, portable artifact, and clean validation.
5. Final documentation and comparison.

## Phase 1: Baseline Profiling and Audit

### Objective

Collect reproducible Release measurements and audit timers, tasks, dependencies, persistence, scans, launch paths, DPI, and accessibility.

### Tasks

- [ ] Rebuild and test in Release.
- [ ] Measure startup, memory, idle CPU, threads, processes, load, launch, scans, and output size.
- [ ] Test simulated libraries from 0 through 250 games.
- [ ] Compare framework-dependent, self-contained, single-file, and ReadyToRun candidates.
- [ ] Record the baseline before source optimizations.

### Success Criteria

Every requested metric is measured or explicitly marked unavailable, and optimization decisions cite observed evidence.

## Phase 2: Targeted Optimization and Robustness

### Objective

Correct demonstrated reliability gaps without adding background work or speculative complexity.

### Tasks

- [ ] Bound log growth without a maintenance timer.
- [ ] Review scanner deduplication and invalid/offline entries.
- [ ] Expand corrupt JSON, shortcut, launch, settings, scanner, and logger tests.
- [ ] Validate repeated settings/discovery/edit cycles for obvious retention.
- [ ] Add explicit Per-Monitor V2 manifest and recheck layout/accessibility.

### Success Criteria

Release remains warning-free, failure paths are covered, idle writes remain zero, and no obvious retention trend remains.

## Phase 3: Release Metadata and Publishing

### Objective

Produce a versioned, self-contained Windows x64 payload from a reusable profile.

### Tasks

- [ ] Configure product, assembly, description, version, copyright, icon, and manifest.
- [ ] Add a self-contained `win-x64` publish profile.
- [ ] Keep trimming and single-file disabled.
- [ ] Decide ReadyToRun from measurements.
- [ ] Add a repeatable release script.

### Success Criteria

The published folder starts without relying on the machine-wide .NET Desktop Runtime and contains no development-only files.

## Phase 4: Installer and Portable Validation

### Objective

Generate and exercise both installer and portable deliverables.

### Tasks

- [ ] Add current-user Inno Setup definition.
- [ ] Compile installer and portable ZIP.
- [ ] Generate SHA-256 checksums.
- [ ] Validate silent install, start, upgrade, uninstall, and reinstall.
- [ ] Confirm program files are removed and `%LOCALAPPDATA%\Playline` is preserved.

### Success Criteria

Real artifacts exist under `artifacts/`, install and uninstall exit successfully, upgrade preserves data, and no Playline process is left behind.

## Phase 5: Final Documentation and Comparison

### Objective

Document verified performance, distribution, regression results, and known limitations.

### Tasks

- [ ] Re-run metrics with the final payload.
- [ ] Complete the before/after table.
- [ ] Write release notes and regression checklist.
- [ ] Update README installation, development, structure, performance, limitations, and roadmap sections.

### Success Criteria

Documentation describes only generated and validated behavior, with exact artifact paths and no unsupported promises.

## Post-Implementation

- [ ] Confirm Registry and process cleanup.
- [ ] Confirm artifacts contain no PDB, test, or source files.
- [ ] Confirm no new launcher or product feature entered scope.

## Native Acrylic Visual Iteration

The final dock pass uses `FluentWpfCore` 1.0.6 only for the HWND material and custom `WindowChrome`. Existing WPF controls, templates, input behavior and persistence remain unchanged. `UseWindowComposition=True` selects the Acrylic composition path supported by Windows 10 1809+; the existing translucent brushes now act as tint and reflections instead of simulating blur.

Validation covers a populated 332 × 90 horizontal bar and a 96 × 222 vertical bar over live, high-contrast backgrounds. The package adds no worker process, watcher, timer or continuous animation. Its MIT notice is copied beside `Playline.exe` in build and publish outputs.

## Notes

- The app icon is a temporary neutral placeholder and must be replaced before 1.0.
- Physical DPI testing is limited to displays present on the validation host; manifest and layout checks supplement it.
