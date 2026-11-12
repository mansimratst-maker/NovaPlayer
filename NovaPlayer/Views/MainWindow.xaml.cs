using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Media.Animation;
using NovaPlayer.ViewModels;
using NovaPlayer.Models;

namespace NovaPlayer.Views;
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private WindowStyle _restoreStyle;
    private ResizeMode _restoreResizeMode;
    private WindowState _restoreWindowState;
    private double _restoreWidth, _restoreHeight, _restoreLeft, _restoreTop;
    private bool _restoreTopmost;
    private GridLength _restoreSidebarColumn, _restoreContentColumn, _restoreTopBarRow, _restorePlaylistColumn;
    private BindingBase? _restorePlaylistColumnBinding;
    private GridLength _restorePlayerRow, _restoreLibraryRow;
    private double _restoreLibraryRowMinHeight;
    private Thickness _restoreContentMargin, _restorePlayerMargin, _restoreRootBorderThickness, _restorePlayerBorderThickness;
    private CornerRadius _restoreRootCornerRadius, _restorePlayerCornerRadius;
    private Visibility _restoreSidebarVisibility, _restoreTopBarVisibility, _restoreLibraryVisibility, _restorePlaylistVisibility;
    private bool _fullscreenLayoutSaved;
    private const uint MonitorDefaultToNearest = 2;
    private const uint SwpNoOwnerZOrder = 0x0200;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmBackdropAcrylic = 3;
    private static readonly nint HwndTopmost = new(-1);
    private static readonly nint HwndNotTopmost = new(-2);
    [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(nint monitor, ref MONITORINFO info);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(nint hwnd, out RECT rect);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int valueSize);
    private readonly DispatcherTimer _controlTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly Dictionary<UIElement, bool> _overlayVisibilityTargets = new();
    private POINT _lastCursorPosition;
    private DateTime _lastControlActivity = DateTime.UtcNow;
    private string _scaleMode = "Fit";
    private string _restoreScaleMode = "Fit";
    private string _fullscreenCropGeometry = "16:9";
    private PictureInPictureWindow? _pipWindow;
    private Point _queueDragPoint;
    private bool _closing;
    private FullscreenOverlayWindow? _fullscreenOverlayWindow;
    private Grid? _fullscreenOverlayOriginalParent;
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent(); _vm = vm; DataContext = vm;
        SourceInitialized += (_, _) => ApplyLiquidGlassBackdrop(DwmBackdropAcrylic);
        StateChanged += (_, _) => { if (MaximizeGlyph is not null) MaximizeGlyph.Kind = WindowState == WindowState.Maximized ? "restore" : "maximize"; };
        Closing += (_, _) => { _closing = true; DetachFullscreenOverlay(); _pipWindow?.Close(); _controlTimer.Stop(); };
        _controlTimer.Tick += (_, _) => PollFullscreenInteraction();
        ComponentDispatcher.ThreadPreprocessMessage += ThreadPreprocessMessage;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsFullscreen)) SetFullscreen(_vm.IsFullscreen);
            else if (e.PropertyName == nameof(MainViewModel.CurrentMedia))
            {
                ApplyVideoScale();
                RefreshPlaybackOverlays();
            }
            else if (e.PropertyName == nameof(MainViewModel.CurrentPage)) ApplyPageVisibility();
            else if (e.PropertyName == nameof(MainViewModel.ArePlaybackControlsVisible)) AnimatePlaybackOverlay(PlaybackDock, _vm.ArePlaybackControlsVisible);
            else if (e.PropertyName == nameof(MainViewModel.ArePlaybackHeaderVisible)) AnimatePlaybackOverlay(PlaybackHeader, _vm.ArePlaybackHeaderVisible);
            else if (e.PropertyName == nameof(MainViewModel.IsPlaying) && !_vm.IsPlaying) RegisterPlayerInteraction();
        };
        ApplyPageVisibility();
        Loaded += (_, _) => RefreshPlaybackOverlays();
        RefreshPlaybackOverlays();
    }
    private void ApplyLiquidGlassBackdrop(int backdropType)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var dark = 1;
            _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
                _ = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdropType, sizeof(int));
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }
    private void SetFullscreen(bool full)
    {
        if (!full)
        {
            _controlTimer.Stop();
            Cursor = Cursors.Arrow;
            DetachFullscreenOverlay();
            _vm.HideFullscreenControls();
            if (!_fullscreenLayoutSaved) return;

            WindowState = WindowState.Normal;
            WindowStyle = _restoreStyle;
            ResizeMode = _restoreResizeMode;
            Topmost = _restoreTopmost;
            ApplyLiquidGlassBackdrop(DwmBackdropAcrylic);
            RootBorder.BorderThickness = _restoreRootBorderThickness;
            RootBorder.CornerRadius = _restoreRootCornerRadius;
            RootBorder.Background = (Brush)FindResource("Canvas");
            Background = (Brush)FindResource("Canvas");
            RootGrid.ColumnDefinitions[0].Width = _restoreSidebarColumn;
            RootGrid.ColumnDefinitions[1].Width = _restoreContentColumn;
            MainContentGrid.RowDefinitions[0].Height = _restoreTopBarRow;
            if (_restorePlaylistColumnBinding is not null)
                BindingOperations.SetBinding(ContentGrid.ColumnDefinitions[1], ColumnDefinition.WidthProperty, _restorePlaylistColumnBinding);
            else
                ContentGrid.ColumnDefinitions[1].Width = _restorePlaylistColumn;
            ContentGrid.Margin = _restoreContentMargin;
            PlayerGrid.Margin = _restorePlayerMargin;
            PlayerGrid.RowDefinitions[0].Height = _restorePlayerRow;
            PlayerGrid.RowDefinitions[1].Height = _restoreLibraryRow;
            PlayerGrid.RowDefinitions[1].MinHeight = _restoreLibraryRowMinHeight;
            PlayerFrame.BorderThickness = _restorePlayerBorderThickness;
            PlayerFrame.CornerRadius = _restorePlayerCornerRadius;
            SidebarPanel.Visibility = _restoreSidebarVisibility;
            TopBar.Visibility = _restoreTopBarVisibility;
            LibraryPane.Visibility = _restoreLibraryVisibility;
            PlaylistPanel.Visibility = _restorePlaylistVisibility;

            Left = _restoreLeft;
            Top = _restoreTop;
            Width = _restoreWidth;
            Height = _restoreHeight;
            WindowState = _restoreWindowState;
            _fullscreenLayoutSaved = false;
            _scaleMode = _restoreScaleMode;
            ScaleModeLabel.Text = _scaleMode;
            ApplyVideoScale();
            return;
        }

        if (_fullscreenLayoutSaved) return;
        if (_vm.CurrentMedia is null)
        {
            _vm.StatusText = "Open a media file before entering fullscreen.";
            _vm.IsFullscreen = false;
            return;
        }
        _pipWindow?.Close();

        _restoreWindowState = WindowState;
        var normalBounds = WindowState == WindowState.Maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
        _restoreLeft = normalBounds.Left;
        _restoreTop = normalBounds.Top;
        _restoreWidth = normalBounds.Width;
        _restoreHeight = normalBounds.Height;
        _restoreStyle = WindowStyle;
        _restoreResizeMode = ResizeMode;
        _restoreTopmost = Topmost;
        _restoreRootBorderThickness = RootBorder.BorderThickness;
        _restoreRootCornerRadius = RootBorder.CornerRadius;
        _restorePlayerBorderThickness = PlayerFrame.BorderThickness;
        _restorePlayerCornerRadius = PlayerFrame.CornerRadius;
        _restoreSidebarColumn = RootGrid.ColumnDefinitions[0].Width;
        _restoreContentColumn = RootGrid.ColumnDefinitions[1].Width;
        _restoreTopBarRow = MainContentGrid.RowDefinitions[0].Height;
        _restorePlaylistColumn = ContentGrid.ColumnDefinitions[1].Width;
        _restorePlaylistColumnBinding = BindingOperations.GetBindingBase(ContentGrid.ColumnDefinitions[1], ColumnDefinition.WidthProperty);
        _restoreContentMargin = ContentGrid.Margin;
        _restorePlayerMargin = PlayerGrid.Margin;
        _restorePlayerRow = PlayerGrid.RowDefinitions[0].Height;
        _restoreLibraryRow = PlayerGrid.RowDefinitions[1].Height;
        _restoreLibraryRowMinHeight = PlayerGrid.RowDefinitions[1].MinHeight;
        _restoreSidebarVisibility = SidebarPanel.Visibility;
        _restoreTopBarVisibility = TopBar.Visibility;
        _restoreLibraryVisibility = LibraryPane.Visibility;
        _restorePlaylistVisibility = PlaylistPanel.Visibility;
        _fullscreenLayoutSaved = true;

        _restoreScaleMode = _scaleMode;
        _scaleMode = "Fill";
        ScaleModeLabel.Text = _scaleMode;

        var hwnd = new WindowInteropHelper(this).Handle;
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (monitor == 0 || !GetMonitorInfo(monitor, ref info))
        {
            _fullscreenLayoutSaved = false;
            _vm.IsFullscreen = false;
            _vm.StatusText = "NovaPlayer couldn't determine this display's fullscreen bounds.";
            return;
        }
        var bounds = info.rcMonitor;
        var dpi = VisualTreeHelper.GetDpi(this);
        // Fullscreen is based on the monitor surface, not on source pixel size.
        // LibVLC scales 480p/720p sources to the output; Fit preserves AR while
        // Fill deliberately crops to the display ratio (default fullscreen choice).
        _fullscreenCropGeometry = $"{Math.Max(1, bounds.Right - bounds.Left)}:{Math.Max(1, bounds.Bottom - bounds.Top)}";

        WindowState = WindowState.Normal;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ApplyLiquidGlassBackdrop(1); // Disable Mica behind the immersive video surface.
        Background = Brushes.Black;
        RootBorder.BorderThickness = new Thickness(0);
        RootBorder.CornerRadius = new CornerRadius(0);
        RootBorder.Background = Brushes.Black;
        RootGrid.ColumnDefinitions[0].Width = new GridLength(0);
        RootGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
        SidebarPanel.Visibility = Visibility.Collapsed;
        MainContentGrid.RowDefinitions[0].Height = new GridLength(0);
        TopBar.Visibility = Visibility.Collapsed;
        ContentGrid.Margin = new Thickness(0);
        ContentGrid.ColumnDefinitions[1].Width = new GridLength(0);
        PlaylistPanel.Visibility = Visibility.Collapsed;
        PlayerGrid.Margin = new Thickness(0);
        PlayerGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
        PlayerGrid.RowDefinitions[1].Height = new GridLength(0);
        // The library row's normal-layout MinHeight otherwise reserves 130 DIPs
        // even with a zero height, producing the black strip beneath fullscreen video.
        PlayerGrid.RowDefinitions[1].MinHeight = 0;
        LibraryPane.Visibility = Visibility.Collapsed;
        PlayerFrame.BorderThickness = new Thickness(0);
        PlayerFrame.CornerRadius = new CornerRadius(0);

        Left = bounds.Left / Math.Max(0.5, dpi.DpiScaleX);
        Top = bounds.Top / Math.Max(0.5, dpi.DpiScaleY);
        Width = Math.Max(1, bounds.Right - bounds.Left) / Math.Max(0.5, dpi.DpiScaleX);
        Height = Math.Max(1, bounds.Bottom - bounds.Top) / Math.Max(0.5, dpi.DpiScaleY);
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            // Apply the physical monitor rectangle after WPF has completed its
            // DPI-aware layout. Applying it before layout lets WPF's later DPI
            // pass shrink the client area (leaving a black strip at the bottom).
            UpdateLayout();
            var handle = new WindowInteropHelper(this).Handle;
            var monitorWidth = bounds.Right - bounds.Left;
            var monitorHeight = bounds.Bottom - bounds.Top;
            SetWindowPos(handle, HwndTopmost, bounds.Left, bounds.Top, monitorWidth, monitorHeight,
                SwpNoOwnerZOrder | SwpFrameChanged | SwpShowWindow);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateLayout();
                if (GetWindowRect(handle, out var actual) &&
                    (actual.Left != bounds.Left || actual.Top != bounds.Top || actual.Right != bounds.Right || actual.Bottom != bounds.Bottom))
                    SetWindowPos(handle, HwndTopmost, bounds.Left, bounds.Top, monitorWidth, monitorHeight,
                        SwpNoOwnerZOrder | SwpFrameChanged | SwpShowWindow);
                UpdateLayout();
                AttachFullscreenOverlay(bounds);
                _fullscreenCropGeometry = $"{Math.Max(1, (int)PrimaryVideoView.ActualWidth)}:{Math.Max(1, (int)PrimaryVideoView.ActualHeight)}";
                ApplyVideoScale();
                Activate();
                Keyboard.Focus(PlayerGrid);
                ShowFullscreenControls();
            }), DispatcherPriority.ContextIdle);
        }), DispatcherPriority.Loaded);
        Activate();
        Focus();
    }
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (!_vm.IsFullscreen && Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase && !IsPointerOverVideoSurface()) return;
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.O) { _vm.OpenFolderCommand.Execute(null); e.Handled = true; return; }
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.O) { _vm.OpenFileCommand.Execute(null); e.Handled = true; return; }
        switch (e.Key)
        {
            case Key.Space: TogglePlaybackWithFeedback(); e.Handled = true; break;
            case Key.Left: SeekWithFeedback(-10_000d); e.Handled = true; break;
            case Key.Right: SeekWithFeedback(10_000d); e.Handled = true; break;
            case Key.Up: _vm.Volume = Math.Min(100, _vm.Volume + 5); e.Handled = true; break;
            case Key.Down: _vm.Volume = Math.Max(0, _vm.Volume - 5); e.Handled = true; break;
            case Key.F: _vm.ToggleFullscreenCommand.Execute(null); e.Handled = true; break;
            case Key.M: _vm.ToggleMuteCommand.Execute(null); e.Handled = true; break;
            case Key.N: _vm.NextCommand.Execute(null); e.Handled = true; break;
            case Key.P: _vm.PreviousCommand.Execute(null); e.Handled = true; break;
            case Key.Escape when _vm.IsFullscreen: _vm.IsFullscreen = false; e.Handled = true; break;
        }
        if (e.Handled) RegisterPlayerInteraction();
    }
    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        // The native LibVLC child can bypass routed input, so the global poll
        // remains the fallback. If WPF receives an actual fullscreen move,
        // reveal controls immediately. The movement threshold rejects synthetic
        // hit-test churn caused by the overlay appearing.
        if (_vm.IsFullscreen)
        {
            if (!GetCursorPos(out var fullscreenPosition) || !IsPositionOverVideoSurface(fullscreenPosition)) return;
            if (Math.Abs(fullscreenPosition.X - _lastCursorPosition.X) < 3 && Math.Abs(fullscreenPosition.Y - _lastCursorPosition.Y) < 3) return;
            RegisterPlayerInteraction();
            return;
        }
        if (!IsPointerOverVideoSurface()) return;
        if (!GetCursorPos(out var position)) return;
        if (Math.Abs(position.X - _lastCursorPosition.X) < 3 && Math.Abs(position.Y - _lastCursorPosition.Y) < 3) return;
        _lastCursorPosition = position;
        RegisterPlayerInteraction();
    }

    private void Player_MouseDown(object sender, MouseButtonEventArgs e)
    {
        PlayerGrid.Focusable = true;
        Keyboard.Focus(PlayerGrid);
        RegisterPlayerInteraction();
    }

    private void RegisterPlayerInteraction()
    {
        if (_vm.CurrentMedia is null) return;
        GetCursorPos(out _lastCursorPosition);
        _lastControlActivity = DateTime.UtcNow;
        Cursor = Cursors.Arrow;
        RevealPlayerControls();
        if (!_controlTimer.IsEnabled) _controlTimer.Start();
    }

    internal void RegisterOverlayActivity() => RegisterPlayerInteraction();

    private void AttachFullscreenOverlay(RECT monitorBounds)
    {
        if (_fullscreenOverlayWindow is not null) return;
        _fullscreenOverlayOriginalParent = PlaybackDock.Parent as Grid;
        if (_fullscreenOverlayOriginalParent is null) return;

        // LibVLC's native video surface owns a separate HWND. Put the controls
        // in an owned transparent window so they always compose above the video,
        // just like the floating transport overlay in Windows Media Player.
        _fullscreenOverlayOriginalParent.Children.Remove(PlaybackHeader);
        _fullscreenOverlayOriginalParent.Children.Remove(PlaybackDock);
        _fullscreenOverlayWindow = new FullscreenOverlayWindow(this, _vm);
        _fullscreenOverlayWindow.MatchOwnerBounds(monitorBounds);
        _fullscreenOverlayWindow.Surface.Children.Add(PlaybackHeader);
        _fullscreenOverlayWindow.Surface.Children.Add(PlaybackDock);
        _fullscreenOverlayWindow.Show();
        var overlayHandle = new WindowInteropHelper(_fullscreenOverlayWindow).Handle;
        SetWindowPos(overlayHandle, HwndTopmost, monitorBounds.Left, monitorBounds.Top,
            monitorBounds.Right - monitorBounds.Left, monitorBounds.Bottom - monitorBounds.Top,
            SwpNoOwnerZOrder | SwpFrameChanged | SwpShowWindow | 0x0010 /* SWP_NOACTIVATE */);
        _fullscreenOverlayWindow.UpdateLayout();
    }

    private void DetachFullscreenOverlay()
    {
        if (_fullscreenOverlayWindow is null) return;
        _fullscreenOverlayWindow.Surface.Children.Remove(PlaybackHeader);
        _fullscreenOverlayWindow.Surface.Children.Remove(PlaybackDock);
        _fullscreenOverlayWindow.Close();
        _fullscreenOverlayWindow = null;
        if (_fullscreenOverlayOriginalParent is not null)
        {
            _fullscreenOverlayOriginalParent.Children.Add(PlaybackHeader);
            _fullscreenOverlayOriginalParent.Children.Add(PlaybackDock);
            _fullscreenOverlayOriginalParent = null;
        }
    }

    // LibVLC's video output is a native child window. WPF routed input can miss
    // events while that child owns focus, so monitor thread keyboard messages.
    private void ThreadPreprocessMessage(ref MSG msg, ref bool handled)
    {
        if (handled || (msg.message != 0x0100 && msg.message != 0x0104)) return; // WM_KEYDOWN / WM_SYSKEYDOWN
        if (!_vm.IsFullscreen && Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase && !IsPointerOverVideoSurface()) return;
        var key = KeyInterop.KeyFromVirtualKey(unchecked((int)msg.wParam));
        if (HandlePlayerShortcut(key)) handled = true;
    }

    // LibVLC renders into a native child HWND, so WPF's IsMouseOver and routed
    // mouse events are not dependable over the actual picture. Use screen space
    // coordinates so player shortcuts still work when the search box last had focus.
    private bool IsPointerOverVideoSurface()
    {
        if (!GetCursorPos(out var cursor)) return false;
        return IsPositionOverVideoSurface(cursor);
    }

    private bool IsPositionOverVideoSurface(POINT cursor)
    {
        if (_vm.IsFullscreen)
        {
            var handle = new WindowInteropHelper(this).Handle;
            var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            return monitor != 0 && GetMonitorInfo(monitor, ref info)
                && cursor.X >= info.rcMonitor.Left && cursor.X < info.rcMonitor.Right
                && cursor.Y >= info.rcMonitor.Top && cursor.Y < info.rcMonitor.Bottom;
        }
        if (PrimaryVideoView.ActualWidth <= 0 || PrimaryVideoView.ActualHeight <= 0) return false;
        try
        {
            var topLeft = PrimaryVideoView.PointToScreen(new Point(0, 0));
            var bottomRight = PrimaryVideoView.PointToScreen(new Point(PrimaryVideoView.ActualWidth, PrimaryVideoView.ActualHeight));
            return cursor.X >= topLeft.X && cursor.X < bottomRight.X && cursor.Y >= topLeft.Y && cursor.Y < bottomRight.Y;
        }
        catch (InvalidOperationException) { return false; }
    }

    private bool HandlePlayerShortcut(Key key)
    {
        switch (key)
        {
            case Key.Space: TogglePlaybackWithFeedback(); break;
            case Key.Left: SeekWithFeedback(-10_000d); break;
            case Key.Right: SeekWithFeedback(10_000d); break;
            case Key.Up: _vm.Volume = Math.Min(100, _vm.Volume + 5); break;
            case Key.Down: _vm.Volume = Math.Max(0, _vm.Volume - 5); break;
            case Key.F: _vm.ToggleFullscreenCommand.Execute(null); break;
            case Key.M: _vm.ToggleMuteCommand.Execute(null); break;
            case Key.N: _vm.NextCommand.Execute(null); break;
            case Key.P: _vm.PreviousCommand.Execute(null); break;
            case Key.Escape when _vm.IsFullscreen: _vm.IsFullscreen = false; break;
            default: return false;
        }
        RegisterPlayerInteraction();
        return true;
    }

    internal bool HandleFullscreenShortcut(Key key) => _vm.IsFullscreen && HandlePlayerShortcut(key);

    private void TogglePlaybackWithFeedback()
    {
        var action = _vm.IsPlaying ? "pause" : "play";
        _vm.TogglePlayPauseCommand.Execute(null);
        _fullscreenOverlayWindow?.ShowPlaybackFeedback(action);
    }

    private void SeekWithFeedback(double deltaMilliseconds)
    {
        if (_vm.SeekBy(deltaMilliseconds))
            _fullscreenOverlayWindow?.ShowPlaybackFeedback(deltaMilliseconds < 0 ? "rewind" : "forward");
        else
            _fullscreenOverlayWindow?.ShowPlaybackFeedback("unavailable");
    }

    private void PlaybackSeekBack_Click(object sender, RoutedEventArgs e)
    {
        _fullscreenOverlayWindow?.ShowPlaybackFeedback(_vm.SeekBy(-10_000d) ? "rewind" : "unavailable");
        RegisterPlayerInteraction();
    }

    private void PlaybackSeekForward_Click(object sender, RoutedEventArgs e)
    {
        _fullscreenOverlayWindow?.ShowPlaybackFeedback(_vm.SeekBy(10_000d) ? "forward" : "unavailable");
        RegisterPlayerInteraction();
    }

    private void PlaybackToggle_Click(object sender, RoutedEventArgs e)
    {
        TogglePlaybackWithFeedback();
        RegisterPlayerInteraction();
    }

    private void RevealPlayerControls()
    {
        if (_vm.IsFullscreen)
        {
            ShowFullscreenControls();
            return;
        }
        if (_vm.CurrentMedia is null) return;
        AnimatePlaybackOverlay(PlaybackDock, true);
        AnimatePlaybackOverlay(PlaybackHeader, true);
    }

    private void RefreshPlaybackOverlays()
    {
        AnimatePlaybackOverlay(PlaybackDock, _vm.ArePlaybackControlsVisible);
        AnimatePlaybackOverlay(PlaybackHeader, _vm.ArePlaybackHeaderVisible);
        if (_vm.CurrentMedia is null)
        {
            _controlTimer.Stop();
            return;
        }
        GetCursorPos(out _lastCursorPosition);
        _lastControlActivity = DateTime.UtcNow;
        if (!_vm.IsPlaying) RevealPlayerControls();
        if (!_controlTimer.IsEnabled) _controlTimer.Start();
    }

    private void ShowFullscreenControls()
    {
        if (!_vm.IsFullscreen) return;
        GetCursorPos(out _lastCursorPosition);
        _lastControlActivity = DateTime.UtcNow;
        Cursor = Cursors.Arrow;
        _vm.ShowFullscreenControls();
        if (!_controlTimer.IsEnabled) _controlTimer.Start();
    }

    private void PollFullscreenInteraction()
    {
        if (_vm.CurrentMedia is null) { _controlTimer.Stop(); return; }
        if (!GetCursorPos(out var position)) return;
        // Activity elsewhere in the desktop must not flash or keep the player
        // controls alive. The timer observes only NovaPlayer's actual video area.
        if (!IsPositionOverVideoSurface(position))
        {
            if (DateTime.UtcNow - _lastControlActivity >= TimeSpan.FromSeconds(3.5)) HideIdlePlayerControls();
            return;
        }
        // WPF/VLC can report a one-pixel cursor wobble while the native video
        // child and overlay exchange hit-testing. Require deliberate movement.
        if (Math.Abs(position.X - _lastCursorPosition.X) >= 3 || Math.Abs(position.Y - _lastCursorPosition.Y) >= 3)
        {
            _lastCursorPosition = position;
            _lastControlActivity = DateTime.UtcNow;
            Cursor = Cursors.Arrow;
            RevealPlayerControls();
        }
        // Paused playback keeps its controls available. While playing, idle
        // controls fade after 3.5 seconds without cursor movement.
        else if (DateTime.UtcNow - _lastControlActivity >= TimeSpan.FromSeconds(3.5)) HideIdlePlayerControls();
    }

    private void HideIdlePlayerControls()
    {
        if (_vm.IsFullscreen)
        {
            if (_vm.IsFullscreenControlsVisible)
            {
                _vm.HideFullscreenControls();
                Cursor = Cursors.None;
            }
        }
        else if (_vm.ArePlaybackControlsVisible)
        {
            AnimatePlaybackOverlay(PlaybackDock, false);
            AnimatePlaybackOverlay(PlaybackHeader, false);
        }
    }
    private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ClickCount == 2) Maximize_Click(sender, e); else DragMove(); }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void SeekSlider_MouseUp(object sender, MouseButtonEventArgs e) { if (sender is System.Windows.Controls.Slider s && _vm.Duration > 0) _vm.SeekToCommand.Execute(s.Value); }
    private void Speed_Click(object sender, RoutedEventArgs e)
    {
        var speeds = new[] { .25, .5, .75, 1d, 1.25, 1.5, 2d, 3d, 4d }; var i = Array.IndexOf(speeds, _vm.PlaybackRate); _vm.PlaybackRate = speeds[(i + 1) % speeds.Length];
    }
    private void MediaCard_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MediaItem item }) _ = _vm.LoadPreviewAsync(item);
    }
    private void QueueItem_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MediaItem item }) _ = _vm.LoadPreviewAsync(item);
    }

    private static MediaItem? ContextItem(object sender)
    {
        if (sender is not System.Windows.Controls.MenuItem item) return null;
        var menu = item.Parent as ContextMenu ?? ItemsControl.ItemsControlFromItemContainer(item) as ContextMenu;
        return (menu?.PlacementTarget as FrameworkElement)?.DataContext as MediaItem ?? item.DataContext as MediaItem;
    }
    private void ContextPlay_Click(object sender, RoutedEventArgs e) { if (ContextItem(sender) is { } item) _vm.PlayItemCommand.Execute(item); }
    private void ContextQueue_Click(object sender, RoutedEventArgs e) => _vm.Enqueue(ContextItem(sender));
    private void ContextFavorite_Click(object sender, RoutedEventArgs e) { if (ContextItem(sender) is { } item) _vm.ToggleFavoriteCommand.Execute(item); }
    private void ContextPlaylist_Click(object sender, RoutedEventArgs e) => _vm.AddMediaToPlaylistCommand.Execute(ContextItem(sender));
    private void ContextRemove_Click(object sender, RoutedEventArgs e) => _vm.RemoveMediaFromLibraryCommand.Execute(ContextItem(sender));
    private void ContextOpenLocation_Click(object sender, RoutedEventArgs e)
    {
        var item = ContextItem(sender);
        if (item is null || !System.IO.File.Exists(item.Path)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{item.Path}\"") { UseShellExecute = true }); }
        catch (Exception ex) { _vm.StatusText = $"File location couldn't be opened: {ex.Message}"; }
    }
    private void ContextProperties_Click(object sender, RoutedEventArgs e)
    {
        if (ContextItem(sender) is not { } item) return;
        MessageBox.Show($"{item.Title}\n\nFormat: {item.Extension}\nSize: {item.SizeText}\nDuration: {item.DurationText}\n\n{item.Path}", "Media properties", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void QueueItem_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not FrameworkElement { DataContext: MediaItem item } element) return;
        var point = e.GetPosition(element);
        if (_queueDragPoint == default) { _queueDragPoint = point; return; }
        if (Math.Abs(point.X - _queueDragPoint.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - _queueDragPoint.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _queueDragPoint = default;
        DragDrop.DoDragDrop(element, new DataObject(typeof(MediaItem), item), DragDropEffects.Move);
    }
    private void QueueItem_Drop(object sender, DragEventArgs e)
    {
        _queueDragPoint = default;
        if (sender is FrameworkElement { DataContext: MediaItem target } && e.Data.GetData(typeof(MediaItem)) is MediaItem moving) _vm.MoveQueueItem(moving, target);
    }
    private void ScaleMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }
    private void ScaleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem { Tag: string mode }) return;
        _scaleMode = mode;
        ScaleModeLabel.Text = mode;
        ApplyVideoScale();
    }
    private void ApplyPageVisibility()
    {
        var settings = !_vm.IsFullscreen && _vm.CurrentPage == "Settings";
        var playlists = !_vm.IsFullscreen && _vm.CurrentPage == "Playlists";
        var hidePlayer = settings || playlists;
        SettingsPage.Visibility = settings ? Visibility.Visible : Visibility.Collapsed;
        PlaylistsPage.Visibility = playlists ? Visibility.Visible : Visibility.Collapsed;
        PlayerGrid.Visibility = hidePlayer ? Visibility.Collapsed : Visibility.Visible;
        PlayerFrame.Visibility = hidePlayer ? Visibility.Collapsed : Visibility.Visible;
        // Keep every player visual out of full-page views. The opaque page surface
        // also prevents media content bleeding through the translucent app shell.
        PrimaryVideoView.Visibility = hidePlayer ? Visibility.Collapsed : Visibility.Visible;
        if (hidePlayer && _fullscreenOverlayWindow is not null) DetachFullscreenOverlay();
        if (hidePlayer && _pipWindow is not null) _pipWindow.Close();
        PlaylistPanel.Visibility = hidePlayer || !_vm.IsPlaylistVisible ? Visibility.Collapsed : Visibility.Visible;
    }
    private void AnimatePlaybackOverlay(UIElement element, bool visible)
    {
        if (_overlayVisibilityTargets.TryGetValue(element, out var currentTarget) && currentTarget == visible)
            return;
        _overlayVisibilityTargets[element] = visible;

        // Keep the in-flight value when reversing a fade. Restarting each
        // mouse-move from opacity 0 made the controls flicker while hovering.
        var opacity = element.Opacity;
        element.BeginAnimation(OpacityProperty, null);
        element.Opacity = opacity;
        var translate = element.RenderTransform as TranslateTransform;
        if (translate is null)
        {
            translate = new TranslateTransform();
            element.RenderTransform = translate;
        }
        translate.BeginAnimation(TranslateTransform.YProperty, null);
        if (visible)
        {
            if (element.Visibility != Visibility.Visible)
            {
                element.Visibility = Visibility.Visible;
                element.Opacity = 0;
                opacity = 0;
            }
            element.BeginAnimation(OpacityProperty, new DoubleAnimation(opacity, 1, TimeSpan.FromMilliseconds(190)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            var startY = translate.Y;
            if (element.Opacity == 0) startY = 10;
            translate.Y = startY;
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(startY, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
        else
        {
            if (element.Visibility != Visibility.Visible || opacity <= 0.01)
            {
                element.Visibility = Visibility.Collapsed;
                return;
            }
            var fade = new DoubleAnimation(opacity, 0, TimeSpan.FromMilliseconds(150));
            fade.Completed += (_, _) =>
            {
                if (_overlayVisibilityTargets.TryGetValue(element, out var target) && !target && element.Opacity <= 0.01)
                    element.Visibility = Visibility.Collapsed;
            };
            element.BeginAnimation(OpacityProperty, fade);
            var fromY = translate.Y;
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(fromY, 8, TimeSpan.FromMilliseconds(150)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });
        }
    }
    private void SettingsBack_Click(object sender, RoutedEventArgs e) => _vm.NavigateCommand.Execute("Home");
    private void TogglePictureInPicture_Click(object sender, RoutedEventArgs e)
    {
        if (_pipWindow is not null) { _pipWindow.Close(); return; }
        if (_vm.CurrentMedia is null) { _vm.StatusText = "Open a media file before starting picture in picture."; return; }
        PrimaryVideoView.MediaPlayer = null;
        MainPlayerHost.Children.Remove(PrimaryVideoView);
        PipPlaceholder.Visibility = Visibility.Visible;
        var window = new PictureInPictureWindow(_vm);
        _pipWindow = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_pipWindow, window)) _pipWindow = null;
            if (_closing) return;
            var view = window.DetachVideoView() ?? PrimaryVideoView;
            if (!MainPlayerHost.Children.Contains(view)) MainPlayerHost.Children.Insert(0, view);
            view.SetBinding(LibVLCSharp.WPF.VideoView.MediaPlayerProperty, new Binding(nameof(MainViewModel.Player)) { Source = _vm });
            PipPlaceholder.Visibility = Visibility.Collapsed;
            _vm.IsPictureInPicture = false;
            Activate();
        };
        window.Show();
        window.AttachVideoView(PrimaryVideoView, _vm.Player);
        _vm.IsPictureInPicture = true;
    }
    private void ApplyVideoScale()
    {
        try
        {
            var player = _vm.Player;
            var target = _fullscreenCropGeometry;
            var scaleView = PrimaryVideoView;
            if (scaleView.ActualWidth > 0 && scaleView.ActualHeight > 0)
                target = $"{Math.Max(1, (int)Math.Round(scaleView.ActualWidth))}:{Math.Max(1, (int)Math.Round(scaleView.ActualHeight))}";
            player.CropGeometry = _scaleMode == "Fill" ? target : string.Empty;
            player.AspectRatio = _scaleMode == "Stretch" ? target : null;
            player.Scale = _scaleMode == "Original" ? 1f : 0f;
        }
        catch { /* LibVLC can reject output scaling changes before a video output exists. */ }
    }
}
