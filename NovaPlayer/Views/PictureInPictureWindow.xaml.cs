using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LibVLCSharp.WPF;
using NovaPlayer.ViewModels;

namespace NovaPlayer.Views;

public partial class PictureInPictureWindow : Window
{
    private VideoView? _mediaView;

    public PictureInPictureWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public void AttachVideoView(VideoView view, LibVLCSharp.Shared.MediaPlayer player)
    {
        if (view.Parent is Panel oldParent) oldParent.Children.Remove(view);
        _mediaView = view;
        VideoHost.Children.Add(view);
        view.MediaPlayer = player;
    }

    public VideoView? DetachVideoView()
    {
        if (_mediaView is null) return null;
        VideoHost.Children.Remove(_mediaView);
        var view = _mediaView;
        _mediaView = null;
        return view;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var node = e.OriginalSource as DependencyObject;
        while (node is not null)
        {
            if (node is Button) return;
            node = VisualTreeHelper.GetParent(node);
        }
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void ReturnToMain_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        switch (e.Key)
        {
            case Key.Space: vm.TogglePlayPauseCommand.Execute(null); break;
            case Key.Left: vm.SeekCommand.Execute(-10_000d); break;
            case Key.Right: vm.SeekCommand.Execute(10_000d); break;
            case Key.Up: vm.Volume = Math.Min(100, vm.Volume + 5); break;
            case Key.Down: vm.Volume = Math.Max(0, vm.Volume - 5); break;
            case Key.M: vm.ToggleMuteCommand.Execute(null); break;
            case Key.N: vm.NextCommand.Execute(null); break;
            case Key.P: vm.PreviousCommand.Execute(null); break;
            case Key.Escape: Close(); break;
            case Key.F:
                Close();
                vm.ToggleFullscreenCommand.Execute(null);
                break;
            default: return;
        }
        e.Handled = true;
    }
}
