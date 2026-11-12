using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NovaPlayer.Database;
using NovaPlayer.MediaEngine;
using NovaPlayer.Models;
using NovaPlayer.Services;

namespace NovaPlayer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IMediaLibraryService _library;
    private readonly LibraryDatabase _database;
    private readonly VlcPlayerService _vlc;
    private readonly IMediaThumbnailService _thumbnails;
    private readonly DispatcherTimer _timer;
    private bool _shuffle;
    private int _queueIndex = -1;
    [ObservableProperty] private string currentPage = "Home";
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private MediaItem? currentMedia;
    [ObservableProperty] private bool isPlaying;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusText = "Your library is ready when you are.";
    [ObservableProperty] private bool isPlaylistVisible = true;
    [ObservableProperty] private bool isFullscreen;
    [ObservableProperty] private bool isFullscreenControlsVisible;
    [ObservableProperty] private bool isPictureInPicture;
    [ObservableProperty] private double volume = 70;
    [ObservableProperty] private double position;
    [ObservableProperty] private double duration;
    [ObservableProperty] private double playbackRate = 1;
    [ObservableProperty] private bool isMuted;
    [ObservableProperty] private int repeatMode;
    [ObservableProperty] private bool hardwareAccelerationEnabled = true;
    [ObservableProperty] private bool rememberPositionEnabled = true;
    [ObservableProperty] private double defaultVolume = 70;
    [ObservableProperty] private string savedPlaylistName = "";
    [ObservableProperty] private SavedPlaylist? selectedPlaylist;
    public ObservableCollection<MediaItem> MediaItems { get; } = [];
    public ObservableCollection<MediaItem> Queue { get; } = [];
    public ObservableCollection<SavedPlaylist> SavedPlaylists { get; } = [];
    public ObservableCollection<MediaItem> SelectedPlaylistItems { get; } = [];
    public ObservableCollection<string> LibraryFolders { get; } = [];
    public string QueueLabel => $"Queue  ·  {Queue.Count}";
    public string RepeatLabel => RepeatMode switch { 1 => "One", 2 => "All", _ => "Off" };
    public LibVLCSharp.Shared.MediaPlayer Player => _vlc.Player;
    public bool ArePlaybackControlsVisible => CurrentMedia is not null && (!IsFullscreen || IsFullscreenControlsVisible);
    public bool ArePlaybackHeaderVisible => ArePlaybackControlsVisible && !IsPictureInPicture;
    public string PositionLabel => TimeText(Position);
    public string DurationLabel => TimeText(Duration);

    public MainViewModel(IMediaLibraryService library, LibraryDatabase database, VlcPlayerService vlc, IMediaThumbnailService thumbnails)
    {
        _library = library; _database = database; _vlc = vlc; _thumbnails = thumbnails;
        Queue.CollectionChanged += (_, _) => OnPropertyChanged(nameof(QueueLabel));
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += async (_, _) =>
        {
            if (Player.IsPlaying) { Position = Math.Max(0, Player.Time); Duration = Math.Max(0, Player.Length); OnPropertyChanged(nameof(PositionLabel)); OnPropertyChanged(nameof(DurationLabel)); }
            if (RememberPositionEnabled && CurrentMedia != null && Player.Time > 0 && Player.Time % 5000 < 500) await _database.SavePositionAsync(CurrentMedia.Path, Player.Time, Player.Length);
        };
        Player.Playing += (_, _) => Application.Current.Dispatcher.Invoke(() => { IsPlaying = true; _timer.Start(); });
        Player.Paused += (_, _) => Application.Current.Dispatcher.Invoke(() => IsPlaying = false);
        Player.Stopped += (_, _) => Application.Current.Dispatcher.Invoke(() => { IsPlaying = false; _timer.Stop(); });
        Player.EncounteredError += (_, _) => Application.Current.Dispatcher.Invoke(() => StatusText = "This media could not be played. The format may be unsupported or the file may be damaged.");
        Player.EndReached += (_, _) => Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (RepeatMode == 1 && CurrentMedia is not null) PlayItem(CurrentMedia);
            else if (Queue.Count > 0 && (_queueIndex + 1 < Queue.Count || RepeatMode == 2)) Next();
            else IsPlaying = false;
        });
        _ = RefreshAsync();
        _ = LoadSettingsAsync();
    }
    private static string TimeText(double ms) => ms > 0 ? TimeSpan.FromMilliseconds(ms).ToString(ms >= 3_600_000 ? @"h:mm:ss" : @"m:ss") : "0:00";
    partial void OnSearchTextChanged(string value) => _ = RefreshAsync();
    partial void OnCurrentPageChanged(string value) { _ = RefreshAsync(); if (value == "Settings") _ = RefreshSettingsAsync(); if (value == "Playlists") _ = RefreshPlaylistsAsync(); }
    partial void OnVolumeChanged(double value) { if (!IsMuted) Player.Volume = (int)value; }
    partial void OnPlaybackRateChanged(double value) { if (Player.IsPlaying) Player.SetRate((float)value); }
    partial void OnPositionChanged(double value) { OnPropertyChanged(nameof(PositionLabel)); }
    partial void OnCurrentMediaChanged(MediaItem? value)
    {
        foreach (var item in Queue) item.IsNowPlaying = value is not null && item.Path.Equals(value.Path, StringComparison.OrdinalIgnoreCase);
        OnPropertyChanged(nameof(ArePlaybackControlsVisible));
        OnPropertyChanged(nameof(ArePlaybackHeaderVisible));
    }
    partial void OnIsFullscreenChanged(bool value) { OnPropertyChanged(nameof(ArePlaybackControlsVisible)); OnPropertyChanged(nameof(ArePlaybackHeaderVisible)); }
    partial void OnIsFullscreenControlsVisibleChanged(bool value) { OnPropertyChanged(nameof(ArePlaybackControlsVisible)); OnPropertyChanged(nameof(ArePlaybackHeaderVisible)); }
    partial void OnIsPictureInPictureChanged(bool value) => OnPropertyChanged(nameof(ArePlaybackHeaderVisible));
    partial void OnHardwareAccelerationEnabledChanged(bool value)
    {
        _vlc.HardwareDecodingEnabled = value;
        try { Player.EnableHardwareDecoding = value; } catch { }
        _ = PersistSettingAsync("HardwareAcceleration", value.ToString());
        if (CurrentMedia is not null) StatusText = "Hardware acceleration preference saved; it is used the next time media opens.";
    }
    partial void OnRememberPositionEnabledChanged(bool value) => _ = PersistSettingAsync("RememberPosition", value.ToString());
    partial void OnDefaultVolumeChanged(double value)
    {
        Volume = Math.Clamp(value, 0, 100);
        _ = PersistSettingAsync("DefaultVolume", Math.Round(value).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
    private async Task PersistSettingAsync(string key, string value)
    {
        try { await _database.SaveSettingAsync(key, value); }
        catch (Exception ex) { StatusText = $"Couldn't save the {key} setting: {ex.Message}"; }
    }
    partial void OnDurationChanged(double value) { OnPropertyChanged(nameof(DurationLabel)); }
    partial void OnRepeatModeChanged(int value) => OnPropertyChanged(nameof(RepeatLabel));
    private async Task LoadSettingsAsync()
    {
        try
        {
            var hardware = await _database.GetSettingAsync("HardwareAcceleration");
            var remember = await _database.GetSettingAsync("RememberPosition");
            var volume = await _database.GetSettingAsync("DefaultVolume");
            var folders = await _library.GetFoldersAsync();
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (bool.TryParse(hardware, out var hw)) HardwareAccelerationEnabled = hw;
                if (bool.TryParse(remember, out var rp)) RememberPositionEnabled = rp;
                if (double.TryParse(volume, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) DefaultVolume = Math.Clamp(v, 0, 100);
                LibraryFolders.Clear(); foreach (var folder in folders) LibraryFolders.Add(folder);
            });
        }
        catch (Exception ex) { StatusText = $"Settings could not be loaded: {ex.Message}"; }
    }
    [RelayCommand] private async Task RefreshSettingsAsync()
    {
        try
        {
            var folders = await _library.GetFoldersAsync();
            await Application.Current.Dispatcher.InvokeAsync(() => { LibraryFolders.Clear(); foreach (var folder in folders) LibraryFolders.Add(folder); });
        }
        catch (Exception ex) { StatusText = $"Folder list could not be refreshed: {ex.Message}"; }
    }
    [RelayCommand] private async Task ResetSettingsAsync()
    {
        try
        {
            await _database.ClearSettingsAsync();
            HardwareAccelerationEnabled = true; RememberPositionEnabled = true; DefaultVolume = 70;
            StatusText = "Nivora Player settings were reset to their defaults.";
        }
        catch (Exception ex) { StatusText = $"Settings could not be reset: {ex.Message}"; }
    }
    private async Task RefreshAsync()
    {
        try { var list = await _library.GetMediaAsync(CurrentPage is "Videos" or "Music" or "Favorites" or "Recently Played" ? CurrentPage : null, SearchText); Application.Current.Dispatcher.Invoke(() => { MediaItems.Clear(); foreach (var i in list) MediaItems.Add(i); }); }
        catch (Exception ex) { StatusText = $"Library refresh failed: {ex.Message}"; }
    }
    [RelayCommand] private void Navigate(string page) => CurrentPage = page;
    [RelayCommand] private async Task OpenFileAsync()
    {
        var dialog = new OpenFileDialog { Title = "Open media", Filter = "Media files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm;*.mp3;*.flac;*.wav;*.m4a;*.ogg;*.aac;*.wma|All files|*.*", Multiselect = true };
        if (dialog.ShowDialog() != true) return;
        try { await _database.AddFilesAsync(dialog.FileNames.Select(x => new FileInfo(x))); await RefreshAsync(); foreach (var f in dialog.FileNames) AddToQueue(new MediaItem { Path = f, SizeBytes = new FileInfo(f).Length }); PlayItem(Queue.Last()); }
        catch (Exception ex) { StatusText = $"Couldn't open media: {ex.Message}"; }
    }
    [RelayCommand] private async Task OpenFolderAsync()
    {
        var dialog = new OpenFolderDialog { Title = "Add a media folder to Nivora Player", Multiselect = false };
        if (dialog.ShowDialog() != true) return;
        IsBusy = true; StatusText = "Scanning folder…";
        try { var count = await _library.ScanFolderAsync(dialog.FolderName); await RefreshAsync(); await RefreshSettingsAsync(); StatusText = $"Added {count:N0} media files."; }
        catch (Exception ex) { StatusText = $"Folder scan failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }
    [RelayCommand] private async Task RefreshLibraryAsync()
    {
        IsBusy = true;
        try { var folders = await _library.GetFoldersAsync(); var count = 0; foreach (var folder in folders.Where(Directory.Exists)) count += await _library.ScanFolderAsync(folder); await RefreshAsync(); await RefreshSettingsAsync(); StatusText = $"Library refreshed · {count:N0} media files found."; }
        catch (Exception ex) { StatusText = $"Refresh failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }
    [RelayCommand] private void PlayItem(MediaItem? item) { if (item is null) return; if (Queue.All(x => x.Path != item.Path)) AddToQueue(item); _queueIndex = Queue.IndexOf(Queue.First(x => x.Path == item.Path)); CurrentMedia = item; var resume = RememberPositionEnabled ? item.LastPositionMs : 0; Position = resume; StatusText = item.Title; try { _vlc.Play(item.Path, resume); } catch (Exception ex) { StatusText = $"Couldn't play {item.Title}: {ex.Message}"; } }
    public void Enqueue(MediaItem? item) { if (item is null) return; AddToQueue(item); StatusText = $"Added {item.Title} to the queue."; }
    private void AddToQueue(MediaItem item) { if (Queue.All(x => x.Path != item.Path)) Queue.Add(item); }
    [RelayCommand] private void TogglePlayPause() { if (Player.Media is null) { if (CurrentMedia is not null) PlayItem(CurrentMedia); else if (MediaItems.Count > 0) PlayItem(MediaItems[0]); } else if (Player.IsPlaying) Player.Pause(); else Player.Play(); }
    [RelayCommand] private void Next() { if (Queue.Count == 0) return; _queueIndex = _shuffle ? Random.Shared.Next(Queue.Count) : (_queueIndex + 1) % Queue.Count; PlayItem(Queue[_queueIndex]); }
    [RelayCommand] private void Previous() { if (Queue.Count == 0) return; if (Player.Time > 3000) { Player.Time = 0; return; } _queueIndex = (_queueIndex - 1 + Queue.Count) % Queue.Count; PlayItem(Queue[_queueIndex]); }
    [RelayCommand] private async Task ToggleFavoriteAsync(MediaItem? item) { if (item is null) return; item.IsFavorite = !item.IsFavorite; await _database.SetFavoriteAsync(item.Path, item.IsFavorite); await RefreshAsync(); }
    [RelayCommand] private void TogglePlaylist() => IsPlaylistVisible = !IsPlaylistVisible;
    [RelayCommand] private void ToggleFullscreen() => IsFullscreen = !IsFullscreen;
    public void ShowFullscreenControls() => IsFullscreenControlsVisible = true;
    public void HideFullscreenControls() => IsFullscreenControlsVisible = false;
    public async Task LoadPreviewAsync(MediaItem item)
    {
        if (!string.IsNullOrEmpty(item.ThumbnailPath)) return;
        var preview = await _thumbnails.GetThumbnailAsync(item);
        if (!string.IsNullOrEmpty(preview)) await Application.Current.Dispatcher.InvokeAsync(() => item.ThumbnailPath = preview);
    }
    [RelayCommand] private void Seek(double delta) => SeekBy(delta);

    public bool SeekBy(double delta)
    {
        if (Player.Media is null) return false;
        var length = Player.Length;
        if (length <= 0)
        {
            StatusText = "This stream does not expose a seekable duration.";
            return false;
        }

        var target = Math.Clamp(Player.Time + (long)delta, 0, length);
        try
        {
            Player.Time = target;
            // Keep the transport readout in sync immediately, including while
            // paused when the periodic playback timer is not updating it.
            Position = target;
            Duration = length;
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"The media engine couldn't seek: {ex.Message}";
            return false;
        }
    }
    [RelayCommand] private void SeekBack() => Seek(-10_000);
    [RelayCommand] private void SeekForward() => Seek(10_000);
    [RelayCommand] private void SeekTo(double value) { if (Player.Length > 0 && Math.Abs(Player.Time - (long)value) > 1000) Player.Time = (long)value; }
    [RelayCommand] private void ToggleMute() { IsMuted = !IsMuted; Player.Mute = IsMuted; }
    [RelayCommand] private void ToggleShuffle() => _shuffle = !_shuffle;
    [RelayCommand] private void CycleRepeat() => RepeatMode = (RepeatMode + 1) % 3;
    [RelayCommand] private void LoadSubtitle()
    {
        var d = new OpenFileDialog { Title = "Load subtitle", Filter = "Subtitle files|*.srt;*.ass;*.ssa;*.sub;*.vtt|All files|*.*" };
        if (d.ShowDialog() == true && !Player.AddSlave(LibVLCSharp.Shared.MediaSlaveType.Subtitle, d.FileName, true)) StatusText = "Subtitle could not be loaded.";
    }
    [RelayCommand] private void ShowTracks()
    {
        var audio = Player.AudioTrackDescription?.Select(t => $"{t.Name} (ID {t.Id})").ToArray() ?? [];
        var text = Player.SpuDescription?.Select(t => $"{t.Name} (ID {t.Id})").ToArray() ?? [];
        MessageBox.Show($"Audio tracks\n{string.Join("\n", audio)}\n\nSubtitle tracks\n{string.Join("\n", text)}\n\nUse the Subtitle button to load an external subtitle.", "Tracks", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    [RelayCommand] private async Task ClearHistoryAsync() { await _database.ClearHistoryAsync(); await RefreshAsync(); StatusText = "Playback history cleared."; }
    [RelayCommand] private async Task RefreshPlaylistsAsync()
    {
        try { var items = await _database.GetPlaylistsAsync(); await Application.Current.Dispatcher.InvokeAsync(() => { SavedPlaylists.Clear(); foreach (var item in items) SavedPlaylists.Add(item); }); }
        catch (Exception ex) { StatusText = $"Playlists could not be loaded: {ex.Message}"; }
    }
    [RelayCommand] private async Task SelectPlaylistAsync(SavedPlaylist? playlist)
    {
        SelectedPlaylist = playlist; SelectedPlaylistItems.Clear();
        if (playlist is null) return;
        SavedPlaylistName = playlist.Name;
        try { var items = await _database.GetPlaylistItemsAsync(playlist.Id); foreach (var item in items) SelectedPlaylistItems.Add(item); StatusText = $"{items.Count:N0} items in {playlist.Name}."; }
        catch (Exception ex) { StatusText = $"Playlist could not be opened: {ex.Message}"; }
    }
    [RelayCommand] private async Task SavePlaylistAsync()
    {
        var name = SavedPlaylistName.Trim(); if (name.Length == 0) { StatusText = "Enter a name for this playlist."; return; }
        try
        {
            var selectedId = SelectedPlaylist?.Id;
            if (SelectedPlaylist is null) selectedId = await _database.CreatePlaylistAsync(name);
            else await _database.RenamePlaylistAsync(SelectedPlaylist.Id, name);
            if (selectedId is long id) await _database.SetPlaylistItemsAsync(id, SelectedPlaylistItems.Select(x => x.Path).ToList());
            await RefreshPlaylistsAsync();
            var refreshed = SavedPlaylists.FirstOrDefault(x => x.Id == selectedId);
            if (refreshed is not null) await SelectPlaylistAsync(refreshed);
            StatusText = "Playlist saved.";
        }
        catch (Exception ex) { StatusText = $"Playlist couldn't be saved: {ex.Message}"; }
    }
    [RelayCommand] private async Task CreatePlaylistAsync()
    {
        SelectedPlaylist = null; SelectedPlaylistItems.Clear();
        if (Queue.Count > 0) foreach (var item in Queue) SelectedPlaylistItems.Add(item);
        if (string.IsNullOrWhiteSpace(SavedPlaylistName)) SavedPlaylistName = "New playlist";
        await SavePlaylistAsync();
    }
    [RelayCommand] private async Task DeletePlaylistAsync(SavedPlaylist? playlist)
    {
        if (playlist is null) return;
        try { await _database.DeletePlaylistAsync(playlist.Id); if (SelectedPlaylist?.Id == playlist.Id) { SelectedPlaylist = null; SelectedPlaylistItems.Clear(); SavedPlaylistName = ""; } await RefreshPlaylistsAsync(); StatusText = $"Removed {playlist.Name}."; }
        catch (Exception ex) { StatusText = $"Playlist couldn't be removed: {ex.Message}"; }
    }
    [RelayCommand] private async Task AddMediaToPlaylistAsync(MediaItem? item)
    {
        if (item is null) return;
        if (SelectedPlaylist is null) { StatusText = "Open a playlist first, then use Add to playlist."; return; }
        if (SelectedPlaylistItems.All(x => !x.Path.Equals(item.Path,StringComparison.OrdinalIgnoreCase))) SelectedPlaylistItems.Add(item);
        await _database.SetPlaylistItemsAsync(SelectedPlaylist.Id, SelectedPlaylistItems.Select(x=>x.Path).ToList());
        await RefreshPlaylistsAsync(); StatusText = $"Added {item.Title} to {SelectedPlaylist.Name}.";
    }
    [RelayCommand] private async Task RemoveMediaFromLibraryAsync(MediaItem? item)
    {
        if (item is null) return;
        try { await _database.RemoveMediaAsync(item.Path); Queue.Remove(Queue.FirstOrDefault(q=>q.Path.Equals(item.Path,StringComparison.OrdinalIgnoreCase))!); await RefreshAsync(); StatusText = $"Removed {item.Title} from the library."; }
        catch (Exception ex) { StatusText = $"Media couldn't be removed: {ex.Message}"; }
    }
    public void MoveQueueItem(MediaItem moving, MediaItem target)
    {
        var from=Queue.IndexOf(moving); var to=Queue.IndexOf(target);
        if(from<0 || to<0 || from==to) return;
        var current = CurrentMedia?.Path;
        Queue.Move(from,to);
        _queueIndex = CurrentMedia is null ? -1 : Queue.ToList().FindIndex(x=>x.Path.Equals(current,StringComparison.OrdinalIgnoreCase));
    }
    [RelayCommand] private async Task RemoveFromPlaylistAsync(MediaItem? item)
    {
        if (item is null || SelectedPlaylist is null) return;
        SelectedPlaylistItems.Remove(item); await _database.SetPlaylistItemsAsync(SelectedPlaylist.Id, SelectedPlaylistItems.Select(x=>x.Path).ToList()); await RefreshPlaylistsAsync();
    }
    [RelayCommand] private async Task MovePlaylistItemUpAsync(MediaItem? item) => await MovePlaylistItemAsync(item,-1);
    [RelayCommand] private async Task MovePlaylistItemDownAsync(MediaItem? item) => await MovePlaylistItemAsync(item,1);
    private async Task MovePlaylistItemAsync(MediaItem? item, int offset)
    {
        if (item is null || SelectedPlaylist is null) return; var index=SelectedPlaylistItems.IndexOf(item); var target=index+offset;
        if (index<0 || target<0 || target>=SelectedPlaylistItems.Count) return;
        SelectedPlaylistItems.Move(index,target); await _database.SetPlaylistItemsAsync(SelectedPlaylist.Id,SelectedPlaylistItems.Select(x=>x.Path).ToList());
    }
    [RelayCommand] private void PlaySelectedPlaylist() { if (SelectedPlaylistItems.Count==0) { StatusText="This playlist is empty."; return; } Queue.Clear(); _queueIndex=-1; foreach(var item in SelectedPlaylistItems) Queue.Add(item); _queueIndex=0; PlayItem(Queue[0]); }
}
