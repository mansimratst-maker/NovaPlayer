using System.Collections.ObjectModel;
using System.IO;
using System.Collections.Concurrent;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LibVLCSharp.Shared;
using NovaPlayer.Database;
using NovaPlayer.Models;
using NovaPlayer.Services;

namespace NovaPlayer.Linux.ViewModels;

public partial class PlayerViewModel : ObservableObject, IDisposable
{
    private readonly LibraryDatabase _database = new();
    private readonly MediaLibraryService _library;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly ConcurrentDictionary<string, Task<string?>> _previewJobs = new(StringComparer.OrdinalIgnoreCase);
    private int _queueIndex = -1;
    private bool _shuffle;
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string status = "Open a file or add a media folder to begin.";
    [ObservableProperty] private MediaItem? currentMedia;
    [ObservableProperty] private bool isPlaying;
    [ObservableProperty] private bool rememberPosition = true;
    [ObservableProperty] private bool isQueueVisible = true;
    [ObservableProperty] private double position;
    [ObservableProperty] private double duration;
    [ObservableProperty] private int volume = 78;
    [ObservableProperty] private int repeatMode;
    [ObservableProperty] private SavedPlaylist? selectedPlaylist;
    public ObservableCollection<SavedPlaylist> SavedPlaylists { get; } = [];
    public ObservableCollection<MediaItem> SelectedPlaylistItems { get; } = [];
    private string? _filter;

    public LibVLC LibVLC { get; }
    public MediaPlayer Player { get; }
    public ObservableCollection<MediaItem> MediaItems { get; } = [];
    public ObservableCollection<MediaItem> Queue { get; } = [];
    public string CurrentTitle => CurrentMedia?.Title ?? "Nothing playing";
    public string QueueTitle => $"Queue  ·  {Queue.Count}";
    public string PlayGlyph => IsPlaying ? "Ⅱ" : "▶";
    public string PositionLabel => FormatTime(Position);
    public string DurationLabel => FormatTime(Duration);
    public string RepeatLabel => RepeatMode switch { 1 => "Repeat one", 2 => "Repeat all", _ => "Repeat off" };

    public PlayerViewModel()
    {
        if (OperatingSystem.IsWindows()) Core.Initialize(Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64"));
        else Core.Initialize();
        LibVLC = new LibVLC("--no-video-title-show", "--file-caching=800", "--network-caching=1200", "--no-snapshot-preview");
        Player = new MediaPlayer(LibVLC) { Volume = volume, EnableHardwareDecoding = true };
        _library = new MediaLibraryService(_database);
        _timer.Tick += (_, _) =>
        {
            if (Player.Length > 0)
            {
                Position = Math.Max(0, Player.Time);
                Duration = Math.Max(0, Player.Length);
                OnPropertyChanged(nameof(PositionLabel)); OnPropertyChanged(nameof(DurationLabel));
            }
        };
        Player.Playing += (_, _) => Dispatcher.UIThread.Post(() => { IsPlaying = true; OnPropertyChanged(nameof(PlayGlyph)); _timer.Start(); });
        Player.Paused += (_, _) => Dispatcher.UIThread.Post(() => { IsPlaying = false; OnPropertyChanged(nameof(PlayGlyph)); });
        Player.Stopped += (_, _) => Dispatcher.UIThread.Post(() => { IsPlaying = false; OnPropertyChanged(nameof(PlayGlyph)); });
        Player.EncounteredError += (_, _) => Dispatcher.UIThread.Post(() => Status = "LibVLC couldn't decode this file. Check the format, file access, and installed codec support.");
        Player.EndReached += (_, _) => Dispatcher.UIThread.Post(AdvanceAtEnd);
        Queue.CollectionChanged += (_, _) => OnPropertyChanged(nameof(QueueTitle));
    }

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        var savedVolume = await _database.GetSettingAsync("volume");
        if (int.TryParse(savedVolume, out var parsedVolume)) Volume = Math.Clamp(parsedVolume, 0, 100);
        var hw = await _database.GetSettingAsync("hardwareAcceleration");
        if (bool.TryParse(hw, out var hardwareAcceleration)) Player.EnableHardwareDecoding = hardwareAcceleration;
        var remember = await _database.GetSettingAsync("rememberPosition");
        if (bool.TryParse(remember, out var shouldRemember)) RememberPosition = shouldRemember;
        await RefreshAsync();
        await RefreshPlaylistsAsync();
    }

    partial void OnSearchTextChanged(string value) => _ = RefreshAsync();
    partial void OnCurrentMediaChanged(MediaItem? value)
    {
        foreach (var item in Queue) item.IsNowPlaying = value != null && string.Equals(item.Path, value.Path, StringComparison.OrdinalIgnoreCase);
        OnPropertyChanged(nameof(CurrentTitle));
    }
    partial void OnIsPlayingChanged(bool value) => OnPropertyChanged(nameof(PlayGlyph));
    partial void OnVolumeChanged(int value) => Player.Volume = Math.Clamp(value, 0, 100);
    partial void OnRepeatModeChanged(int value) => OnPropertyChanged(nameof(RepeatLabel));

    public async Task RefreshAsync()
    {
        try
        {
            var result = await _library.GetMediaAsync(_filter, SearchText);
            await Dispatcher.UIThread.InvokeAsync(() => { MediaItems.Clear(); foreach (var item in result) MediaItems.Add(item); });
            Status = result.Count == 0 ? "Your collection is ready when you are." : $"{result.Count:N0} media files in your collection.";
        }
        catch (Exception ex) { Status = $"Library refresh failed: {ex.Message}"; }
    }

    public async Task AddFilesAsync(IEnumerable<string> paths)
    {
        var files = paths.Where(File.Exists).Select(path => new FileInfo(path)).ToList();
        if (files.Count == 0) return;
        await _database.AddFilesAsync(files);
        await RefreshAsync();
        foreach (var file in files) AddToQueue(new MediaItem { Path = file.FullName, SizeBytes = file.Length });
        PlayItem(Queue.LastOrDefault(x => files.Any(f => string.Equals(f.FullName, x.Path, StringComparison.OrdinalIgnoreCase))));
    }

    public async Task AddFolderAsync(string path)
    {
        Status = "Scanning folder…";
        try
        {
            var count = await _library.ScanFolderAsync(path);
            await RefreshAsync();
            Status = $"Added {count:N0} media files from the selected folder.";
        }
        catch (Exception ex) { Status = $"Folder scan failed: {ex.Message}"; }
    }

    public void SetFilter(string? filter) { _filter = filter; _ = RefreshAsync(); }
    public void AddToQueue(MediaItem? item) { if (item != null && Queue.All(x => !string.Equals(x.Path, item.Path, StringComparison.OrdinalIgnoreCase))) { Queue.Add(item); OnPropertyChanged(nameof(QueueTitle)); } }

    public async Task LoadPreviewAsync(MediaItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.ThumbnailPath) || !MediaLibraryService.VideoExtensions.Contains(Path.GetExtension(item.Path))) return;
        var preview = await _previewJobs.GetOrAdd(item.Path, GeneratePreviewAsync);
        if (preview is not null) await Dispatcher.UIThread.InvokeAsync(() => item.ThumbnailPath = preview);
    }
    private async Task<string?> GeneratePreviewAsync(string path)
    {
        try
        {
            var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaPlayer", "Thumbnails");
            Directory.CreateDirectory(cache);
            var output = Path.Combine(cache, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path))) + ".jpg");
            if (File.Exists(output)) return output;
            var start = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-ss", "00:00:05", "-i", path, "-frames:v", "1", "-vf", "scale=480:-2", "-y", output }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start);
            if (process is null) return null;
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            return process.ExitCode == 0 && File.Exists(output) ? output : null;
        }
        catch { return null; }
    }

    public void PlayItem(MediaItem? item)
    {
        if (item is null || !File.Exists(item.Path)) { Status = "This media file is missing or no longer accessible."; return; }
        AddToQueue(item);
        _queueIndex = Queue.IndexOf(Queue.First(x => string.Equals(x.Path, item.Path, StringComparison.OrdinalIgnoreCase)));
        CurrentMedia = Queue[_queueIndex]; Position = Math.Max(0, RememberPosition ? item.LastPositionMs : 0); Duration = Math.Max(0, item.DurationMs);
        try
        {
            using var media = new Media(LibVLC, item.Path, FromType.FromPath);
            Player.Play(media);
            if (RememberPosition && item.LastPositionMs > 5000) Player.Time = item.LastPositionMs;
            Status = item.Title;
            _ = _database.SavePositionAsync(item.Path, item.LastPositionMs, item.DurationMs);
        }
        catch (Exception ex) { Status = $"Couldn't play {item.Title}: {ex.Message}"; }
    }

    public void TogglePlayPause() { if (Player.Media is null && CurrentMedia != null) PlayItem(CurrentMedia); else if (Player.IsPlaying) Player.Pause(); else Player.Play(); }
    public void Seek(long delta) { if (Player.Length > 0) Player.Time = Math.Clamp(Player.Time + delta, 0, Player.Length); }
    public void SeekTo(double value) { if (Player.Length > 0) Player.Time = Math.Clamp((long)value, 0, Player.Length); }
    public void Next() { if (Queue.Count == 0) return; _queueIndex = _shuffle ? Random.Shared.Next(Queue.Count) : (_queueIndex + 1) % Queue.Count; PlayItem(Queue[_queueIndex]); }
    public void Previous() { if (Player.Time > 3000) { Player.Time = 0; return; } if (Queue.Count > 0) { _queueIndex = (_queueIndex - 1 + Queue.Count) % Queue.Count; PlayItem(Queue[_queueIndex]); } }
    public void ToggleShuffle() => _shuffle = !_shuffle;
    public void CycleRepeat() => RepeatMode = (RepeatMode + 1) % 3;
    public void ToggleQueue() => IsQueueVisible = !IsQueueVisible;

    public async Task RefreshPlaylistsAsync()
    {
        var items = await _database.GetPlaylistsAsync();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            SavedPlaylists.Clear();
            foreach (var playlist in items) SavedPlaylists.Add(playlist);
            if (SelectedPlaylist is not null) SelectedPlaylist = SavedPlaylists.FirstOrDefault(x => x.Id == SelectedPlaylist.Id);
        });
    }
    public async Task CreatePlaylistAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) { Status = "Enter a playlist name."; return; }
        try { await _database.CreatePlaylistAsync(name); await RefreshPlaylistsAsync(); Status = $"Created playlist {name.Trim()}."; }
        catch (Exception ex) { Status = $"Couldn't create playlist: {ex.Message}"; }
    }
    public async Task SelectPlaylistAsync(SavedPlaylist? playlist)
    {
        SelectedPlaylist = playlist; SelectedPlaylistItems.Clear();
        if (playlist is null) return;
        foreach (var item in await _database.GetPlaylistItemsAsync(playlist.Id)) SelectedPlaylistItems.Add(item);
    }
    public async Task AddMediaToPlaylistAsync(MediaItem item, SavedPlaylist? playlist)
    {
        if (playlist is null) { Status = "Create or select a playlist first."; return; }
        var paths = (await _database.GetPlaylistItemsAsync(playlist.Id)).Select(x => x.Path).ToList();
        if (!paths.Contains(item.Path, StringComparer.OrdinalIgnoreCase)) paths.Add(item.Path);
        await _database.SetPlaylistItemsAsync(playlist.Id, paths); await RefreshPlaylistsAsync();
        if (SelectedPlaylist?.Id == playlist.Id) await SelectPlaylistAsync(SelectedPlaylist);
        Status = $"Added {item.Title} to {playlist.Name}.";
    }
    public async Task SaveSelectedPlaylistAsync(string name)
    {
        if (SelectedPlaylist is null) return;
        try
        {
            await _database.RenamePlaylistAsync(SelectedPlaylist.Id, name);
            await _database.SetPlaylistItemsAsync(SelectedPlaylist.Id, SelectedPlaylistItems.Select(x => x.Path).ToList());
            await RefreshPlaylistsAsync(); Status = "Playlist saved.";
        }
        catch (Exception ex) { Status = $"Couldn't save playlist: {ex.Message}"; }
    }
    public async Task DeleteSelectedPlaylistAsync()
    {
        if (SelectedPlaylist is null) return;
        await _database.DeletePlaylistAsync(SelectedPlaylist.Id); SelectedPlaylist = null; SelectedPlaylistItems.Clear(); await RefreshPlaylistsAsync();
    }
    public void PlaySelectedPlaylist()
    {
        if (SelectedPlaylistItems.Count == 0) { Status = "This playlist is empty."; return; }
        Queue.Clear(); foreach (var item in SelectedPlaylistItems) Queue.Add(item);
        _queueIndex = 0; PlayItem(Queue[0]);
    }
    public async Task RemoveFromSelectedPlaylistAsync(MediaItem item)
    {
        if (SelectedPlaylist is null) return;
        SelectedPlaylistItems.Remove(item);
        await _database.SetPlaylistItemsAsync(SelectedPlaylist.Id, SelectedPlaylistItems.Select(x => x.Path).ToList());
        await RefreshPlaylistsAsync();
    }
    public async Task PersistSettingsAsync(bool hardwareAcceleration)
    {
        Player.EnableHardwareDecoding = hardwareAcceleration;
        await _database.SaveSettingAsync("hardwareAcceleration", hardwareAcceleration.ToString());
        await _database.SaveSettingAsync("volume", Volume.ToString());
        await _database.SaveSettingAsync("rememberPosition", RememberPosition.ToString());
    }

    private void AdvanceAtEnd()
    {
        if (RepeatMode == 1 && CurrentMedia != null) PlayItem(CurrentMedia);
        else if (Queue.Count > 0 && (_queueIndex + 1 < Queue.Count || RepeatMode == 2)) Next();
        else { IsPlaying = false; OnPropertyChanged(nameof(PlayGlyph)); }
    }
    private static string FormatTime(double ms) => ms > 0 ? TimeSpan.FromMilliseconds(ms).ToString(ms >= 3_600_000 ? @"h\:mm\:ss" : @"m\:ss") : "0:00";
    public void Dispose() { _timer.Stop(); Player.Stop(); Player.Dispose(); LibVLC.Dispose(); }
}
