using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using LibVLCSharp.Shared;
using NovaPlayer.MediaEngine;
using NovaPlayer.Models;

namespace NovaPlayer.Services;

public sealed class MediaThumbnailService(VlcPlayerService vlc) : IMediaThumbnailService
{
    private readonly ConcurrentDictionary<string, Task<string?>> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _renderer = new(1, 1);
    private readonly string _cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaPlayer", "Thumbnails");

    public async Task<string?> GetThumbnailAsync(MediaItem item, CancellationToken cancellationToken = default)
    {
        if (!MediaLibraryService.VideoExtensions.Contains(Path.GetExtension(item.Path)) || !File.Exists(item.Path)) return null;
        Directory.CreateDirectory(_cache);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(item.Path).ToUpperInvariant())));
        var target = Path.Combine(_cache, key + ".png");
        if (File.Exists(target) && new FileInfo(target).Length > 1024) return target;
        var task = _pending.GetOrAdd(key, _ => GenerateAsync(item.Path, target, cancellationToken));
        var result = await task;
        if (result is null && _pending.TryGetValue(key, out var current) && ReferenceEquals(current, task)) _pending.TryRemove(key, out _);
        return result;
    }

    private async Task<string?> GenerateAsync(string sourcePath, string targetPath, CancellationToken cancellationToken)
    {
        HwndSource? output = null;
        MediaPlayer? player = null;
        var acquired = false;
        try
        {
            await _renderer.WaitAsync(cancellationToken);
            acquired = true;
            var args = await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var parameters = new HwndSourceParameters("NovaPlayer Thumbnail Renderer")
                {
                    Width = 480, Height = 270, PositionX = -30000, PositionY = -30000,
                    WindowStyle = unchecked((int)0x80000000), ExtendedWindowStyle = 0x80,
                    UsesPerPixelOpacity = false
                };
                output = new HwndSource(parameters);
                return output.Handle;
            });
            player = new MediaPlayer(vlc.LibVLC) { Hwnd = args, EnableHardwareDecoding = false };
            var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void onPlaying(object? _, EventArgs __) => ready.TrySetResult(true);
            void onError(object? _, EventArgs __) => ready.TrySetResult(false);
            player.Playing += onPlaying; player.EncounteredError += onError;
            using (var media = new Media(vlc.LibVLC, sourcePath, FromType.FromPath)) player.Play(media);
            var completed = await Task.WhenAny(ready.Task, Task.Delay(TimeSpan.FromSeconds(12), cancellationToken));
            if (completed != ready.Task || !await ready.Task || cancellationToken.IsCancellationRequested) return null;
            if (player.Length > 60_000) player.Time = Math.Min(30_000, player.Length / 4);
            await Task.Delay(1100, cancellationToken);
            var saved = await Task.Run(() => player.TakeSnapshot(0, targetPath, 480, 270), cancellationToken);
            player.Playing -= onPlaying; player.EncounteredError -= onError;
            player.Stop();
            return saved && File.Exists(targetPath) && new FileInfo(targetPath).Length > 1024 ? targetPath : null;
        }
        catch { return null; }
        finally
        {
            try { player?.Stop(); player?.Dispose(); } catch { }
            if (output is not null) await Application.Current.Dispatcher.InvokeAsync(output.Dispose);
            if (acquired) _renderer.Release();
        }
    }
}
