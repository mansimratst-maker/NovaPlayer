using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using NovaPlayer.Linux.ViewModels;
using NovaPlayer.Models;

namespace NovaPlayer.Linux.Views;

public partial class MainWindow : Window
{
    private readonly PlayerViewModel _vm = new();
    private readonly DispatcherTimer _overlayTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly DispatcherTimer _fadeOutTimer = new() { Interval = TimeSpan.FromMilliseconds(280) };
    private Point _lastPointer;
    private DateTime _lastActivity = DateTime.UtcNow;
    private bool _hasPointer;
    private bool _pointerOverPlayer;
    private string _scale = "Fit";
    private string _restoreScale = "Fit";
    private Window? _pipWindow;
    private bool _fullscreen;
    private bool _closing;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        AddHandler(KeyDownEvent, Window_KeyDown, RoutingStrategies.Tunnel);
        // VideoView may render in its own native child window. Listen at the top-level
        // window as well as the overlay so pointer movement remains visible to us.
        AddHandler(PointerMovedEvent, Window_PointerMoved, RoutingStrategies.Tunnel);
        Opened += async (_, _) =>
        {
            await _vm.InitializeAsync();
            if (Program.LaunchMediaPaths.Length > 0) await _vm.AddFilesAsync(Program.LaunchMediaPaths);
        };
        Closing += (_, _) => { _closing = true; _overlayTimer.Stop(); _fadeOutTimer.Stop(); _pipWindow?.Close(); _vm.Dispose(); };
        _overlayTimer.Tick += (_, _) => PollPointer();
        _fadeOutTimer.Tick += (_, _) =>
        {
            _fadeOutTimer.Stop();
            if (DateTime.UtcNow - _lastActivity >= TimeSpan.FromSeconds(6.5))
            {
                QuickControls.IsVisible = false;
                OverlayHeader.IsVisible = false;
            }
        };
        _vm.PropertyChanged += Vm_PropertyChanged;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerViewModel.CurrentMedia))
        {
            EmptyState.IsVisible = _vm.CurrentMedia is null;
            OverlayHeader.IsVisible = _vm.CurrentMedia is not null;
            if (_vm.CurrentMedia is not null) ShowControls();
        }
        else if (e.PropertyName == nameof(PlayerViewModel.IsPlaying) && !_vm.IsPlaying) ShowControls();
        if (e.PropertyName == nameof(PlayerViewModel.IsPlaying)) PlayPauseIcon.Kind = _vm.IsPlaying ? "pause" : "play";
    }

    private void PlayerOverlay_PointerMoved(object? sender, PointerEventArgs e)
    {
        _lastPointer = e.GetPosition(VideoSurface);
        _pointerOverPlayer = true;
        ShowControls();
    }

    private void PlayerOverlay_PointerExited(object? sender, PointerEventArgs e) => _pointerOverPlayer = false;
    private void Window_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (VideoHost.Bounds.Width <= 0 || VideoHost.Bounds.Height <= 0) return;
        var point = e.GetPosition(VideoHost);
        var overVideo = point.X >= 0 && point.Y >= 0 && point.X <= VideoHost.Bounds.Width && point.Y <= VideoHost.Bounds.Height;
        if (!overVideo) { _pointerOverPlayer = false; return; }
        if (!_hasPointer || Math.Abs(point.X - _lastPointer.X) > 1 || Math.Abs(point.Y - _lastPointer.Y) > 1)
        {
            _hasPointer = true;
            _lastPointer = point;
            _pointerOverPlayer = true;
            ShowControls();
        }
    }
    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e) => BeginMoveDrag(e);

    private void ShowControls()
    {
        if (_vm.CurrentMedia is null) return;
        _lastActivity = DateTime.UtcNow;
        _fadeOutTimer.Stop();
        OverlayHeader.IsVisible = true;
        QuickControls.IsVisible = true;
        OverlayHeader.Opacity = 1;
        QuickControls.Opacity = 1;
        if (!_overlayTimer.IsEnabled) _overlayTimer.Start();
    }

    private void PollPointer()
    {
        if (DateTime.UtcNow - _lastActivity >= TimeSpan.FromSeconds(6.5))
        {
            QuickControls.Opacity = 0;
            OverlayHeader.Opacity = 0;
            if (!_fadeOutTimer.IsEnabled) _fadeOutTimer.Start();
        }
    }

    private async void OpenFiles_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open media",
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("Video and audio") { Patterns = ["*.mp4", "*.mkv", "*.avi", "*.mov", "*.wmv", "*.webm", "*.mp3", "*.flac", "*.wav", "*.m4a", "*.ogg", "*.aac", "*.opus"] }, FilePickerFileTypes.All]
        });
        if (files.Count > 0) await _vm.AddFilesAsync(files.Select(x => x.Path.LocalPath));
    }

    private async void AddFolder_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Add a media folder", AllowMultiple = false });
        if (folders.Count > 0) await _vm.AddFolderAsync(folders[0].Path.LocalPath);
    }

    private void Refresh_Click(object? sender, RoutedEventArgs e) { _vm.SetFilter(null); ShowControls(); }
    private void NavigateHome_Click(object? sender, RoutedEventArgs e) => _vm.SetFilter(null);
    private void FilterVideos_Click(object? sender, RoutedEventArgs e) => _vm.SetFilter("Videos");
    private void FilterMusic_Click(object? sender, RoutedEventArgs e) => _vm.SetFilter("Music");
    private void History_Click(object? sender, RoutedEventArgs e) => _vm.SetFilter("Recently Played");
    private void Favorites_Click(object? sender, RoutedEventArgs e) => _vm.SetFilter("Favorites");
    private void Search_TextChanged(object? sender, TextChangedEventArgs e) => _vm.SearchText = SearchBox.Text ?? "";
    private void LibraryItem_Click(object? sender, RoutedEventArgs e) { if (sender is Button { DataContext: MediaItem item }) _vm.PlayItem(item); ShowControls(); }
    private void QueueItem_Click(object? sender, RoutedEventArgs e) { if (sender is Button { DataContext: MediaItem item }) _vm.PlayItem(item); ShowControls(); }
    private void LibraryCard_Loaded(object? sender, RoutedEventArgs e) { if (sender is Button { DataContext: MediaItem item }) _ = _vm.LoadPreviewAsync(item); }
    private void QueueCard_Loaded(object? sender, RoutedEventArgs e) { if (sender is Button { DataContext: MediaItem item }) _ = _vm.LoadPreviewAsync(item); }
    private void PlayPause_Click(object? sender, RoutedEventArgs e) { _vm.TogglePlayPause(); ShowControls(); }
    private void Previous_Click(object? sender, RoutedEventArgs e) { _vm.Previous(); ShowControls(); }
    private void Next_Click(object? sender, RoutedEventArgs e) { _vm.Next(); ShowControls(); }
    private void SeekBack_Click(object? sender, RoutedEventArgs e) { _vm.Seek(-10_000); ShowControls(); }
    private void SeekForward_Click(object? sender, RoutedEventArgs e) { _vm.Seek(10_000); ShowControls(); }
    private void Timeline_Released(object? sender, PointerReleasedEventArgs e) { _vm.SeekTo(Timeline.Value); ShowControls(); }
    private void Mute_Click(object? sender, RoutedEventArgs e) { _vm.Player.Mute = !_vm.Player.Mute; ShowControls(); }
    private void Shuffle_Click(object? sender, RoutedEventArgs e) { _vm.ToggleShuffle(); ShowControls(); }
    private void Repeat_Click(object? sender, RoutedEventArgs e) { _vm.CycleRepeat(); ShowControls(); }
    private void ToggleQueue_Click(object? sender, RoutedEventArgs e) => _vm.ToggleQueue();

    private async void Settings_Click(object? sender, RoutedEventArgs e)
    {
        var acceleration = new ToggleSwitch { Content = "Use hardware decoding when supported", IsChecked = _vm.Player.EnableHardwareDecoding, Foreground = Avalonia.Media.Brushes.White };
        var remember = new ToggleSwitch { Content = "Remember playback position", IsChecked = _vm.RememberPosition, Foreground = Avalonia.Media.Brushes.White };
        var volume = new Slider { Minimum = 0, Maximum = 100, Value = _vm.Volume, Width = 280 };
        volume.ValueChanged += (_, args) => _vm.Volume = (int)args.NewValue;
        var save = new Button { Content = "Save settings", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Padding = new Thickness(18, 9), Background = Avalonia.Media.Brushes.MediumPurple, Foreground = Avalonia.Media.Brushes.White };
        var window = new Window { Title = "NovaPlayer · Settings", Width = 440, Height = 350, MinWidth = 400, MinHeight = 300, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0C1224")), Foreground = Avalonia.Media.Brushes.White };
        save.Click += async (_, _) => { _vm.RememberPosition = remember.IsChecked == true; await _vm.PersistSettingsAsync(acceleration.IsChecked == true); window.Close(); _vm.Status = "Settings saved."; };
        var content = new StackPanel { Margin = new Thickness(26), Spacing = 18 };
        content.Children.Add(new TextBlock { Text = "Settings", FontSize = 24, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        content.Children.Add(new TextBlock { Text = "PLAYBACK", Foreground = Avalonia.Media.Brushes.MediumPurple, FontSize = 11, FontWeight = Avalonia.Media.FontWeight.Bold });
        content.Children.Add(acceleration);
        content.Children.Add(remember);
        content.Children.Add(new TextBlock { Text = "Default volume" }); content.Children.Add(volume);
        content.Children.Add(new TextBlock { Text = "Your library, playlists, and preferences are stored locally in SQLite.", TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = Avalonia.Media.Brushes.LightGray });
        content.Children.Add(save); window.Content = content; await window.ShowDialog(this);
    }

    private async void Playlists_Click(object? sender, RoutedEventArgs e)
    {
        await _vm.RefreshPlaylistsAsync();
        var name = new TextBox { Watermark = "Playlist name", MinWidth = 230 };
        var selector = new ComboBox { ItemsSource = _vm.SavedPlaylists, DisplayMemberBinding = new Avalonia.Data.Binding("Name"), MinWidth = 230, PlaceholderText = "Choose a playlist" };
        var tracks = new ListBox { ItemsSource = _vm.SelectedPlaylistItems, DisplayMemberBinding = new Avalonia.Data.Binding("Title"), MinHeight = 140 };
        selector.SelectionChanged += async (_, _) => { await _vm.SelectPlaylistAsync(selector.SelectedItem as SavedPlaylist); name.Text = _vm.SelectedPlaylist?.Name ?? ""; };
        var window = new Window { Title = "NovaPlayer · Playlists", Width = 520, Height = 560, MinWidth = 420, MinHeight = 440, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0C1224")), Foreground = Avalonia.Media.Brushes.White };
        var buttons = new WrapPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
        Button Make(string label, EventHandler<RoutedEventArgs> action) { var b = new Button { Content = label, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(12, 8), Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#222C49")), Foreground = Avalonia.Media.Brushes.White }; b.Click += action; return b; }
        buttons.Children.Add(Make("Create", async (_, _) => { await _vm.CreatePlaylistAsync(name.Text ?? ""); await _vm.RefreshPlaylistsAsync(); selector.SelectedItem = _vm.SavedPlaylists.LastOrDefault(); }));
        buttons.Children.Add(Make("Save / rename", async (_, _) => { await _vm.SaveSelectedPlaylistAsync(name.Text ?? ""); await _vm.RefreshPlaylistsAsync(); }));
        buttons.Children.Add(Make("Add current track", async (_, _) => { if (_vm.CurrentMedia is not null) await _vm.AddMediaToPlaylistAsync(_vm.CurrentMedia, selector.SelectedItem as SavedPlaylist); await _vm.SelectPlaylistAsync(selector.SelectedItem as SavedPlaylist); }));
        buttons.Children.Add(Make("Remove selected track", async (_, _) => { if (tracks.SelectedItem is MediaItem item) await _vm.RemoveFromSelectedPlaylistAsync(item); }));
        buttons.Children.Add(Make("Play playlist", (_, _) => _vm.PlaySelectedPlaylist()));
        buttons.Children.Add(Make("Delete playlist", async (_, _) => { await _vm.DeleteSelectedPlaylistAsync(); await _vm.RefreshPlaylistsAsync(); }));
        var layout = new StackPanel { Margin = new Thickness(24), Spacing = 12 };
        layout.Children.Add(new TextBlock { Text = "Your playlists", FontSize = 23, FontWeight = Avalonia.Media.FontWeight.SemiBold }); layout.Children.Add(name); layout.Children.Add(selector); layout.Children.Add(tracks); layout.Children.Add(buttons);
        window.Content = layout; await window.ShowDialog(this);
    }

    private async void Subtitle_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Select subtitle", AllowMultiple = false, FileTypeFilter = [new FilePickerFileType("Subtitles") { Patterns = ["*.srt", "*.ass", "*.ssa", "*.vtt", "*.sub"] }] });
        if (files.Count > 0 && !_vm.Player.AddSlave(MediaSlaveType.Subtitle, files[0].Path.LocalPath, true)) _vm.Status = "This subtitle could not be loaded.";
        ShowControls();
    }

    private void Fullscreen_Click(object? sender, RoutedEventArgs e) => ToggleFullscreen();
    private void ToggleFullscreen()
    {
        _fullscreen = !_fullscreen;
        WindowState = _fullscreen ? WindowState.FullScreen : WindowState.Normal;
        if (_fullscreen) { _restoreScale = _scale; _scale = "Fill"; }
        else _scale = _restoreScale;
        ScaleButton.Content = $"{_scale}  ⌄";
        var size = VideoSurface.Bounds.Size;
        var ratio = size.Height > 0 ? $"{(int)size.Width}:{(int)size.Height}" : "16:9";
        _vm.Player.CropGeometry = _fullscreen && _scale == "Fill" ? ratio : "";
        _vm.Player.AspectRatio = null;
        ShowControls();
    }

    private void ScaleMode_Click(object? sender, RoutedEventArgs e)
    {
        _scale = _scale switch { "Fit" => "Fill", "Fill" => "Stretch", _ => "Fit" };
        ScaleButton.Content = $"{_scale}  ⌄";
        var size = VideoSurface.Bounds.Size;
        var ratio = size.Height > 0 ? $"{(int)size.Width}:{(int)size.Height}" : "16:9";
        _vm.Player.CropGeometry = _scale == "Fill" ? ratio : "";
        _vm.Player.AspectRatio = _scale == "Stretch" ? ratio : null;
        ShowControls();
    }

    private void Pip_Click(object? sender, RoutedEventArgs e)
    {
        if (_pipWindow is not null) { _pipWindow.Close(); return; }
        if (_vm.CurrentMedia is null) { _vm.Status = "Open media before entering picture in picture."; return; }
        MainLayoutGrid.Children.Remove(VideoHost);
        _pipWindow = new Window { Title = $"NovaPlayer · {_vm.CurrentTitle}", Width = 650, Height = 390, MinWidth = 350, MinHeight = 220, Topmost = true, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#080C19")), Content = VideoHost };
        _pipWindow.Closed += (_, _) =>
        {
            var window = _pipWindow; _pipWindow = null;
            if (_closing || window is null) return;
            window.Content = null;
            Grid.SetRow(VideoHost, 0); Grid.SetColumn(VideoHost, 0);
            MainLayoutGrid.Children.Insert(0, VideoHost);
            ShowControls();
        };
        _pipWindow.Show();
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!_fullscreen && SearchBox.IsFocused && !_pointerOverPlayer) return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.O) { _ = OpenFiles_ClickAsync(); e.Handled = true; return; }
        switch (e.Key)
        {
            case Key.Space: _vm.TogglePlayPause(); ShowControls(); e.Handled = true; break;
            case Key.Left: _vm.Seek(-10_000); ShowControls(); e.Handled = true; break;
            case Key.Right: _vm.Seek(10_000); ShowControls(); e.Handled = true; break;
            case Key.Up: _vm.Volume = Math.Min(100, _vm.Volume + 5); ShowControls(); e.Handled = true; break;
            case Key.Down: _vm.Volume = Math.Max(0, _vm.Volume - 5); ShowControls(); e.Handled = true; break;
            case Key.F: ToggleFullscreen(); e.Handled = true; break;
            case Key.M: Mute_Click(null, new RoutedEventArgs()); e.Handled = true; break;
            case Key.N: _vm.Next(); ShowControls(); e.Handled = true; break;
            case Key.P: _vm.Previous(); ShowControls(); e.Handled = true; break;
            case Key.Escape when _fullscreen: ToggleFullscreen(); e.Handled = true; break;
        }
    }

    private async Task OpenFiles_ClickAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Open media", AllowMultiple = true, FileTypeFilter = [FilePickerFileTypes.All] });
        if (files.Count > 0) await _vm.AddFilesAsync(files.Select(x => x.Path.LocalPath));
    }
}
