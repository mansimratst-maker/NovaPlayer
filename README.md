# Nivora Player

**Every Frame. Every Sound. Beautifully Played.**

Nivora Player is a LibVLC powered media player with a Windows 11 WPF interface and a separate Avalonia desktop client for Linux. Both clients use a local SQLite library and support media formats provided by their LibVLC engine.

## Downloads

Releases are published at [GitHub Releases](https://github.com/mansimratst-maker/NovaPlayer/releases). The Windows installer is a per-user `.exe`; the Arch/CachyOS bundle contains a PKGBUILD and Linux application payload.

## Windows installation

1. Download `NivoraPlayer-Setup-1.1.1-x64.exe` from the release page and run it. Administrator access is not required.
2. Launch Nivora Player from Start. Open a file or add a media folder to the library.
3. Uninstall from **Settings → Apps → Installed apps**, or run `unins000.exe` in `%LOCALAPPDATA%\Programs\NovaPlayer`.

Nivora Player keeps its library and preferences under `%LOCALAPPDATA%\NovaPlayer` to preserve existing data from the previous NovaPlayer name. The app uses the included Windows LibVLC runtime.

## Arch Linux and CachyOS

Download and extract `NivoraPlayer-Arch-x64.zip`, then run:

```bash
makepkg -si
```

This builds and installs the native package, launcher, and app icon. VLC, its plugins, and FFmpeg are provided by the system.

## Features

- LibVLC playback for local files and supported network streams.
- Searchable SQLite media library with folder scanning and preview thumbnails.
- Queue, playlists, favorites, recent history, position resume, subtitles, audio tracks, playback speed, shuffle, and repeat.
- Windowed, fullscreen, and picture-in-picture playback.
- Fullscreen uses the monitor's output dimensions, independent of source resolution. Fit preserves the source aspect ratio; Fill scales to the display and crops excess edges. 480p and 720p sources scale to the selected display normally.
- Audio-only media gets a dedicated animated visualizer stage instead of the LibVLC blank video output.
- Mouse activity outside the player surface does not trigger or reset the player control overlay.

## Build from source

Requires the .NET 10 SDK and network access for NuGet restore.

Windows:

```powershell
dotnet build NovaPlayer\NovaPlayer.csproj -c Release -r win-x64
dotnet publish NovaPlayer\NovaPlayer.csproj -c Release -r win-x64 --self-contained true -o dist\NivoraPlayer-Windows-x64\NivoraPlayer
```

Linux:

```bash
dotnet build NovaPlayer.Linux/NovaPlayer.Linux.csproj -c Release
dotnet publish NovaPlayer.Linux/NovaPlayer.Linux.csproj -c Release -r linux-x64 --self-contained true -o dist/NivoraPlayer-Linux-x64
```

The Windows desktop app uses WPF. The Linux desktop client is separate because WPF is Windows-only. This repository does not currently contain an Android app or APK build target.

## Windows security notice

Direct-download Windows executables are not signed yet. Microsoft SmartScreen can show an unknown-publisher warning for a new or unsigned download. A trusted Authenticode certificate or Microsoft Store distribution is required to establish publisher reputation; a self-signed certificate does not solve this for other users. See [Microsoft's SmartScreen guidance](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation).

## Project layout

- `NovaPlayer/` — Windows WPF application, MVVM, LibVLC, SQLite, media library, fullscreen and PiP.
- `NovaPlayer.Linux/` — Linux Avalonia desktop client.
- `packaging/windows/` — Windows installer and maintenance scripts.
- `packaging/arch/` — Arch/CachyOS PKGBUILD, launcher, and icon.

## Third-party software

See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for licenses and dependency notices.
