# Playline Foundation Research

## Overview

Phase 1 establishes a small Windows desktop application that can load and persist a game library without implementing launcher discovery or the final bar interface.

## Problem Statement

Playline needs a maintainable base that keeps UI, library rules, and local JSON persistence separate while remaining idle when the user is not interacting with it.

## Recommended Approach

- Target .NET 10 and WPF on Windows 10/11 x64.
- Keep the domain and library service in a platform-independent class library.
- Put `System.Text.Json` repositories and local application paths in a storage library.
- Use the WPF application only as the composition root.
- Use ordinary constructor injection instead of a dependency-injection container.
- Persist data below `%LOCALAPPDATA%\Playline` and create files lazily during initialization.

## Data Requirements

- `games.json` contains a root object with a `games` array.
- `settings.json` contains the initial application settings.
- `cache/` is created for future image and icon caching.
- `logs/` receives entries only for important failures.

## Risks and Mitigations

- Missing, empty, or malformed files: return safe defaults and log malformed JSON.
- Partial writes: write to a temporary sibling file and atomically replace the destination.
- Unnecessary runtime work: do not introduce timers, watchers, background services, or scanning.
- Scope creep: exclude all discovery, launching, and final UI work from this phase.

## References

- [Install .NET on Windows](https://learn.microsoft.com/dotnet/core/install/windows)
- [WPF desktop guide](https://learn.microsoft.com/dotnet/desktop/wpf/)
- [.NET project SDK overview](https://learn.microsoft.com/dotnet/core/project-sdk/overview)

