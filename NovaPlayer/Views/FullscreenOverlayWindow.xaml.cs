using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NovaPlayer.ViewModels;

namespace NovaPlayer.Views;

public partial class FullscreenOverlayWindow : Window
{
    private readonly MainWindow _playerWindow;
    private readonly DispatcherTimer _feedbackTimer = new() { Interval = TimeSpan.FromMilliseconds(850) };
    private int _feedbackSequence;

    public FullscreenOverlayWindow(MainWindow playerWindow, MainViewModel viewModel)
    {
        InitializeComponent();
        _playerWindow = playerWindow;
        DataContext = viewModel;
        Owner = playerWindow;
        OverlaySurface.MouseMove += (_, _) => _playerWindow.RegisterOverlayActivity();
        OverlaySurface.PreviewMouseDown += (_, _) => _playerWindow.RegisterOverlayActivity();
        _feedbackTimer.Tick += (_, _) =>
        {
            _feedbackTimer.Stop();
            var sequence = _feedbackSequence;
            var fade = new DoubleAnimation(PlaybackFeedback.Opacity, 0, TimeSpan.FromMilliseconds(230))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            fade.Completed += (_, _) =>
            {
                if (sequence == _feedbackSequence && PlaybackFeedback.Opacity < .01)
                    PlaybackFeedback.Visibility = Visibility.Collapsed;
            };
            PlaybackFeedback.BeginAnimation(OpacityProperty, fade);
            FeedbackScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(FeedbackScale.ScaleX, .92, TimeSpan.FromMilliseconds(230)));
            FeedbackScale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(FeedbackScale.ScaleY, .92, TimeSpan.FromMilliseconds(230)));
        };
    }

    public Grid Surface => OverlaySurface;

    public void ShowPlaybackFeedback(string action)
    {
        _feedbackSequence++;
        _feedbackTimer.Stop();
        switch (action)
        {
            case "unavailable":
                FeedbackIcon.Kind = "info";
                FeedbackTitle.Text = "Can’t seek this stream";
                FeedbackDetail.Text = "THE MEDIA DOESN’T PROVIDE A DURATION";
                break;
            case "rewind":
                FeedbackIcon.Kind = "rewind";
                FeedbackTitle.Text = "10 seconds back";
                FeedbackDetail.Text = "REWIND";
                break;
            case "forward":
                FeedbackIcon.Kind = "forward";
                FeedbackTitle.Text = "10 seconds ahead";
                FeedbackDetail.Text = "SKIP FORWARD";
                break;
            case "pause":
                FeedbackIcon.Kind = "pause";
                FeedbackTitle.Text = "Paused";
                FeedbackDetail.Text = "SPACE TO RESUME";
                break;
            default:
                FeedbackIcon.Kind = "play";
                FeedbackTitle.Text = "Playing";
                FeedbackDetail.Text = "SPACE TO PAUSE";
                break;
        }
        if (PlaybackFeedback.Visibility != Visibility.Visible)
        {
            PlaybackFeedback.Visibility = Visibility.Visible;
            PlaybackFeedback.Opacity = 0;
            FeedbackScale.ScaleX = .82;
            FeedbackScale.ScaleY = .82;
        }
        PlaybackFeedback.BeginAnimation(OpacityProperty,
            new DoubleAnimation(PlaybackFeedback.Opacity, 1, TimeSpan.FromMilliseconds(160))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        FeedbackScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(FeedbackScale.ScaleX, 1, TimeSpan.FromMilliseconds(210))
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = .18 } });
        FeedbackScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(FeedbackScale.ScaleY, 1, TimeSpan.FromMilliseconds(210))
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = .18 } });
        _feedbackTimer.Start();
    }

    internal void MatchOwnerBounds(MainWindow.RECT bounds)
    {
        var dpi = VisualTreeHelper.GetDpi(_playerWindow);
        Left = bounds.Left / Math.Max(.5, dpi.DpiScaleX);
        Top = bounds.Top / Math.Max(.5, dpi.DpiScaleY);
        Width = Math.Max(1, bounds.Right - bounds.Left) / Math.Max(.5, dpi.DpiScaleX);
        Height = Math.Max(1, bounds.Bottom - bounds.Top) / Math.Max(.5, dpi.DpiScaleY);
    }

    private void FullscreenOverlay_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_playerWindow.HandleFullscreenShortcut(e.Key)) e.Handled = true;
    }
}
