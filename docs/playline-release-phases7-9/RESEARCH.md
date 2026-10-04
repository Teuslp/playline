# Playline Release Phases 7–9 Research

## Overview

Finalize the initial Playline cycle through measured profiling, targeted robustness work, and a reproducible Windows x64 distribution. The scope is stabilization rather than new launcher or product features.

## Problem Statement

The application is functionally complete through Phase 6, but it has no repeatable release pipeline, product metadata, application icon, installer, checksums, or controlled log growth. Performance and large-library behavior also need real measurements before changes are justified.

## User Stories / Use Cases

- A user installs Playline without first installing .NET or granting administrator rights.
- An upgrade replaces program files while preserving `%LOCALAPPDATA%\Playline`.
- A user can unzip a portable build and run the same tested binaries.
- Corrupt launcher data, JSON, icons, shortcuts, or missing drives do not crash the bar.
- A maintainer can reproduce startup, library-load, scan, launch, memory, and idle-CPU measurements.

## Technical Research

### Publishing Options

#### Framework-dependent

- Very small application payload.
- Requires the matching .NET Desktop Runtime to be installed.
- Appropriate for development but adds friction to a first public release.

#### Self-contained folder

- Includes the .NET Desktop Runtime and works on a clean compatible Windows installation.
- Larger, but the installer compresses the folder and can update all files atomically enough for this desktop use case.
- Does not require runtime extraction during every launch.

#### Self-contained single-file

- Simplifies manual copying, but WPF still depends on native runtime components that must be bundled/extracted for a true single file.
- The measured candidate is larger than a compressed installer and starts slightly slower.
- Adds no meaningful benefit when both installer and ZIP already hide file count from users.

#### ReadyToRun

- Can reduce JIT work at startup, at the cost of larger managed assemblies.
- The measured size delta is small because the self-contained framework binaries are already platform-specific.
- Retain only if repeated measurements show neutral or improved startup.

#### Trimming

- Rejected. Microsoft documents WPF and Windows Forms as not trim-compatible because of reflection and built-in COM marshalling.

### Installer

Use Inno Setup with `PrivilegesRequired=lowest` and `{localappdata}\Programs\Playline`:

- no administrative prompt;
- Start Menu shortcut plus optional desktop shortcut;
- stable `AppId` for upgrades;
- application files are removed on uninstall;
- user library and settings remain under `%LOCALAPPDATA%\Playline` and are intentionally preserved.

### Persistence and Logs

JSON already uses a unique temporary file followed by an overwrite move. Add bounded critical logging because daily files can currently grow without limit. Rotation should happen only when an error is written; no background maintenance is needed.

### Performance Strategy

- Measure Release builds only.
- Use the same optimized, isolated build before and after code changes.
- Separate UI startup from pure library-load and scanner timing.
- Test 0, 1, 10, 50, 100, and 250 simulated games.
- Do not add virtualization unless large-library measurements show an unacceptable practical regression and the interaction cost is justified.

### Acrylic Composition Decision

WPF does not expose Acrylic or Mica controllers of its own. A simulated stack of translucent gradients could reproduce the colors but not blur the desktop behind the window. The selected `FluentWpfCore` 1.0.6 package is a focused 54 KB assembly with a .NET 10 target, an MIT license, `WindowChrome` compatibility and separate composition paths for Windows 10 and Windows 11.

The Playline window uses its Acrylic material with Windows composition enabled and keeps all controls in the existing WPF layer. This avoids a WinUI migration and does not import a full component suite. A translucent WPF tint remains available as the visual fallback if the operating system cannot render the backdrop.

References:

- [WPF Fluent appearance architecture](https://github.com/dotnet/wpf/blob/main/Documentation/docs/using-fluent.md)
- [FluentWpfCore source and usage](https://github.com/TwilightLemon/FluentWpfCore)
- [FluentWpfCore NuGet package](https://www.nuget.org/packages/FluentWpfCore)

## Recommended Approach

- Version the release as `0.9.0-beta` because it is the first distributable build, unsigned, and has only one physical DPI environment available for final validation.
- Publish self-contained Windows x64 as a normal folder.
- Compare ReadyToRun and retain it only if measurements justify it.
- Keep single-file and trimming disabled.
- Add a Per-Monitor V2 application manifest.
- Add simple log size/retention limits and broaden failure-path tests.
- Generate both a current-user Inno Setup installer and a portable ZIP from the same published folder.
- Generate SHA-256 checksums and concise release notes.

## Required Technologies

- .NET 10 SDK publishing for `win-x64`.
- WPF/WinForms Windows Desktop shared framework.
- Inno Setup compiler.
- PowerShell `Compress-Archive` and `Get-FileHash` for portable/checksum artifacts.
- Existing xUnit test project and a small diagnostics console.

## Data Requirements

No production schema additions are required. Diagnostic data is temporary and release metrics are recorded under this documentation directory.

## UI/UX Considerations

- Preserve the compact bar and all Phase 1–6 behavior.
- Provide a clean placeholder app icon, documented for later replacement.
- Verify keyboard focus, accessible names, tooltips, contrast, and tray recovery.
- Avoid fragile WPF UI unit tests; use focused UI automation for regression flows.

## Integration Points

- `Playline.App.csproj`: metadata, manifest, icon, publish properties.
- `CriticalFileLogger`: bounded on-demand rotation.
- Steam/Epic scanners and shortcut/launch services: failure-path tests.
- `installer/`: Inno Setup definition.
- `scripts/`: deterministic release build and validation.
- `artifacts/`: generated deliverables only, excluded from source control.

## Risks and Challenges

- Self-contained output is substantially larger than framework-dependent output.
- Unsigned installer and binaries can trigger SmartScreen reputation warnings.
- Mixed-DPI correctness cannot be exhaustively proven on a single 100% scale display.
- Changing to a virtualized horizontal list could regress drag/drop and keyboard navigation; avoid without strong evidence.
- Silent installer tests must restore any startup Registry state and preserve real user data.

## Open Questions

None blocking. A future release should replace the placeholder icon and add code signing before declaring 1.0 stable.

## References

- [Microsoft .NET application publishing overview](https://learn.microsoft.com/dotnet/core/deploying/)
- [Microsoft single-file deployment](https://learn.microsoft.com/dotnet/core/deploying/single-file/overview)
- [Microsoft trimming incompatibilities](https://learn.microsoft.com/dotnet/core/deploying/trimming/incompatibilities)
- [Microsoft DPI awareness manifest guidance](https://learn.microsoft.com/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process)
- [Microsoft .NET support on Windows](https://learn.microsoft.com/dotnet/core/install/windows)
- [Inno Setup downloads](https://jrsoftware.org/isdl.php)
- [Inno Setup non-administrative mode](https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm)
- [Inno Setup command-line parameters](https://jrsoftware.org/ishelp/topic_setupcmdline.htm)
