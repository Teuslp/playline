# Playline Functional Phases 3–4 Research

## Overview

These phases add manual game management, local icon caching, safe launching, and on-demand discovery for Steam and Epic Games.

## Recommended Architecture

- Evolve `Game.Id` to a stable string and add `Arguments` and `WorkingDirectory`.
- Keep identity normalization and library mutation rules in `Playline.Core`.
- Add `Playline.Windows` for Shell Link resolution, executable metadata, icons, and process launching.
- Add `Playline.Discovery` for Steam/Epic location, parsing, and scan aggregation.
- Keep file dialogs and small auxiliary windows in `Playline.App`.
- Run scanners only from the explicit **Procurar jogos** action.

## Manual Games

- Resolve `.lnk` through native Shell Link COM interfaces (`IShellLinkW` and `IPersistFile`).
- Read friendly executable names through `FileVersionInfo`.
- Derive a stable manual ID from the normalized executable path.
- Extract a 64-pixel local icon once and cache it as PNG.
- Launch executables with a structured `ProcessStartInfo`; use the shell only for registered launcher URIs.

## Steam

- Locate Steam through the user/machine registry plus standard fallback locations.
- Parse all libraries listed by `steamapps/libraryfolders.vdf`.
- Parse `appmanifest_*.acf`, requiring app ID, name, install directory, and an existing install location.
- Use `steam://rungameid/{appId}` and ID `steam-{appId}`.

## Epic Games

- Locate `.item` manifests in the system CommonApplicationData directory and supported launcher PCB-mode locations.
- Require a complete install, display name, stable manifest identifier, and an existing install directory.
- Prefer the launcher URI when catalog namespace/item/artifact data exists; retain the local executable as fallback metadata.

## Error and Performance Strategy

- Missing launchers and directories return empty results.
- A malformed manifest is skipped without aborting its scanner.
- A failed scanner is logged once while the remaining scanners continue.
- No watcher, timer, scheduler, worker, or persistent task is introduced.

## References

- [Shell Link format and COM management](https://learn.microsoft.com/openspecs/windows_protocols/ms-shllink/a6c2f32d-2297-4727-bcd3-5d3669573bcb)
- [ProcessStartInfo](https://learn.microsoft.com/dotnet/api/system.diagnostics.processstartinfo)
- [SHGetFileInfoW icon extraction](https://learn.microsoft.com/windows/win32/api/shellapi/nf-shellapi-shgetfileinfow)
- [Steam URL launch parameters](https://partner.steamgames.com/doc/api/isteamapps)
- [Epic launcher manifests and PCB mode](https://dev.epicgames.com/documentation/unreal-engine/multiple-launcher-unreal-engine-installs)
