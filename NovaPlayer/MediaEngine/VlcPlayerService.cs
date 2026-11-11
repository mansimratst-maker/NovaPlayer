using LibVLCSharp.Shared;

namespace NovaPlayer.MediaEngine;
public sealed class VlcPlayerService : IDisposable
{
    public LibVLC LibVLC { get; }
    public MediaPlayer Player { get; }
    public bool HardwareDecodingEnabled { get; set; } = true;
    public VlcPlayerService()
    {
        LibVLC = new LibVLC("--avcodec-hw=any", "--no-video-title-show", "--file-caching=1000", "--network-caching=1500");
        Player = new MediaPlayer(LibVLC);
        // Keep Win32 video input routed through WPF so fullscreen Escape, hotkeys,
        // and pointer movement can reveal the overlay instead of being swallowed by VLC.
        Player.EnableKeyInput = false;
        Player.EnableMouseInput = false;
    }
    public void Play(string path, long resumeMs = 0)
    {
        Player.EnableHardwareDecoding = HardwareDecodingEnabled;
        using var media = Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsAbsoluteUri && uri.Scheme is "http" or "https" ? new Media(LibVLC, uri) : new Media(LibVLC, path, FromType.FromPath);
        Player.Play(media);
        if (resumeMs > 5000) Player.Time = resumeMs;
    }
    public void Dispose() { Player.Stop(); Player.Dispose(); LibVLC.Dispose(); }
}
