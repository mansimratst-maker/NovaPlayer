# NovaPlayer

**Every Frame. Every Sound. Beautifully Played.**

NovaPlayer is a dark, cinematic desktop media player using LibVLC for playback. It pairs a custom Windows 11 WPF interface with a Linux Avalonia target for Arch Linux and CachyOS. Both use the same LibVLC engine family and local SQLite library database.

## Downloads

- [Windows x64 setup installer (.exe)](https://github.com/mansimratst-maker/NovaPlayer/releases/download/v1.0.3/NovaPlayer-Setup-1.0.3-x64.exe)
- [Arch Linux / CachyOS x64 package bundle](https://github.com/mansimratst-maker/NovaPlayer/releases/download/v1.0.0/NovaPlayer-Arch-x64.zip)
- [Linux x64 portable payload](https://github.com/mansimratst-maker/NovaPlayer/releases/download/v1.0.0/NovaPlayer-linux-x64.tar.gz)

## Install on Windows 10/11 x64

1. Download and run `NovaPlayer-Setup-1.0.1-x64.exe`. Administrator rights are not required.
2. The installer adds Start menu and optional desktop shortcuts. Open NovaPlayer and add a media folder or choose **Open file**.
3. To uninstall, use Windows **Settings → Apps → Installed apps** or run `unins000.exe` in `%LOCALAPPDATA%\Programs\NovaPlayer`. Your library, settings, history, and preview cache remain in `%LOCALAPPDATA%\NovaPlayer`.

The installer is self-contained and includes the Windows LibVLC runtime. A portable ZIP is also available on the release page.

## Install on Arch Linux or CachyOS x86_64

Download and extract `NovaPlayer-Arch-x64.zip`. It contains the Linux application payload, a PKGBUILD, and the desktop icon and launcher. In the extracted directory run:

```bash
makepkg -si
```

`makepkg` builds and installs a native Arch package. NovaPlayer installs under `/opt/novaplayer` and gets an application-menu entry with the NovaPlayer icon. The package uses the system VLC libraries and plugins, so codec availability depends on the installed VLC build and its enabled modules. FFmpeg is used to generate cached video previews.

## Playback and controls

- LibVLC handles local video and audio formats, streams supported by the installed engine, track selection, external subtitles, seeking, volume, and speed controls.
- Add folders to the SQLite library. NovaPlayer generates and caches video-frame previews for library and queue cards. Windows uses LibVLC; Linux uses FFmpeg.
- Filter Home, Videos, Music, Recently Played, and Favorites from the left navigation.
- Create named playlists, edit their names and contents, reorder tracks, and play them.
- Drag queue rows to reorder. Right-click a library item for play, queue, favorite, playlist, file-location, properties, and remove actions.
- Playback controls reveal on pointer/keyboard activity and fade after 6.5 seconds without activity, including while paused. Fullscreen uses a borderless monitor-sized window; `F` toggles it and `Esc` exits.
- Picture in picture opens a resizable always-on-top player window.

Keyboard shortcuts: **Space** play/pause, **Left/Right** seek 10 seconds, **Up/Down** volume, **F** fullscreen, **Esc** exit fullscreen, **M** mute, **N/P** next/previous, **Ctrl+O** open files, **Ctrl+Shift+O** add a folder.

## Build from source

Requirements: .NET 10 SDK, network access for NuGet restore, and VLC development/runtime libraries on Linux.

```powershell
dotnet build NovaPlayer\NovaPlayer.csproj -c Release -r win-x64
dotnet publish NovaPlayer\NovaPlayer.csproj -c Release -r win-x64 --self-contained true -o dist\NovaPlayer-Windows-x64\NovaPlayer
```

```bash
dotnet build NovaPlayer.Linux/NovaPlayer.Linux.csproj -c Release
dotnet publish NovaPlayer.Linux/NovaPlayer.Linux.csproj -c Release -r linux-x64 --self-contained true -o dist/NovaPlayer-Linux-x64
```

## Project layout

- `NovaPlayer/` — Windows WPF client, MVVM playback and settings, vector icon system, LibVLC player, SQLite library, media previews, fullscreen and picture in picture.
- `NovaPlayer.Linux/` — Linux desktop client using Avalonia and LibVLCSharp.Avalonia; shares the media model, SQLite database, folder scanner, and saved playlists.
- `packaging/windows/` — per-user Windows install and uninstall scripts.
- `packaging/arch/` — Arch/CachyOS PKGBUILD and desktop entry.

## Codec and platform notes

LibVLC's available formats are determined by the VLC engine, its plugins, hardware, and the media's codec/DRM. NovaPlayer reports playback errors for files the installed engine cannot decode. Windows ships VideoLAN's Windows runtime. Arch/CachyOS use the system VLC package. The Linux UI target is separate because WPF is a Windows-only desktop framework.

## Third-party components

NovaPlayer uses LibVLCSharp and VLC/LibVLC under their respective LGPL terms, Avalonia under the MIT license, and Microsoft.Data.Sqlite. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
