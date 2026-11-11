using System.Windows;
using System.Windows.Media;

namespace NovaPlayer.Controls;

/// <summary>Central Lucide-inspired vector icon used throughout the NovaPlayer UI.</summary>
public sealed class NovaIcon : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(string), typeof(NovaIcon), new FrameworkPropertyMetadata("circle", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(NovaIcon), new FrameworkPropertyMetadata(24d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(NovaIcon), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(NovaIcon), new FrameworkPropertyMetadata(1.8d, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly IReadOnlyDictionary<string, Geometry> Icons = CreateIcons();
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (!Icons.TryGetValue(Kind, out var geometry)) geometry = Icons["circle"];
        var scale = Math.Min(ActualWidth, ActualHeight) / 24d;
        if (scale <= 0) return;
        var pen = new Pen(Stroke, StrokeThickness / scale) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.DrawGeometry(null, pen, geometry);
        dc.Pop();
    }

    private static IReadOnlyDictionary<string, Geometry> CreateIcons()
    {
        // Lucide-style 24-unit paths keep stroke, alignment, and DPI behavior consistent.
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["chevron-down"] = "m6 9 6 6 6-6",
            ["sparkles"] = "m12 3 1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8z M19 14l1 2.5 2.5 1-2.5 1L19 21l-1-2.5-2.5-1 2.5-1z",
            ["home"] = "M3 10.5 12 3l9 7.5 M5 9v11h5v-6h4v6h5V9",
            ["library"] = "M4 4h16v16H4z M8 8h8 M8 12h8 M8 16h5",
            ["video"] = "M3 7h13v10H3z M16 10l5-3v10l-5-3z",
            ["music"] = "M9 18V5l12-2v13 M9 18a3 3 0 1 1-3-3 3 3 0 0 1 3 3Zm12-2a3 3 0 1 1-3-3 3 3 0 0 1 3 3Z",
            ["history"] = "M3 12a9 9 0 1 0 2.64-6.36L3 8 M3 3v5h5 M12 7v5l3 2",
            ["star"] = "m12 3 2.7 5.5 6.1.9-4.4 4.3 1 6.1-5.4-2.9-5.4 2.9 1-6.1-4.4-4.3 6.1-.9z",
            ["settings"] = "M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8Zm0-5v2m0 14v2m9-9h-2M5 12H3m15.36-6.36-1.42 1.42M7.06 16.94l-1.42 1.42m12.72 0-1.42-1.42M7.06 7.06 5.64 5.64",
            ["minimize"] = "M5 12h14",
            ["maximize"] = "M5 5h14v14H5z",
            ["restore"] = "M8 8V4h12v12h-4 M4 8h12v12H4z",
            ["close"] = "m18 6-12 12M6 6l12 12",
            ["search"] = "M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16Zm10 2-4.35-4.35",
            ["folder"] = "M3 6a2 2 0 0 1 2-2h5l2 2h7a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z",
            ["folder-plus"] = "M3 6a2 2 0 0 1 2-2h5l2 2h7a2 2 0 0 1 2 2v5 M3 10h13 M19 16v6m-3-3h6",
            ["file-plus"] = "M13 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h8m-1-20v6h6 M16 18h6m-3-3v6m-1-19 6 6",
            ["play"] = "m8 5 12 7-12 7z",
            ["pause"] = "M8 5v14M16 5v14",
            ["stop"] = "M6 6h12v12H6z",
            ["previous"] = "M19 5v14M17 12 5 5v14z",
            ["next"] = "M5 5v14M7 12l12-7v14z",
            ["rewind"] = "m11 19-9-7 9-7v14Zm10 0-9-7 9-7v14Z",
            ["forward"] = "m13 5 9 7-9 7V5ZM3 5l9 7-9 7V5Z",
            ["volume"] = "M11 5 6 9H3v6h3l5 4z M15.5 8.5a5 5 0 0 1 0 7m3-10a9 9 0 0 1 0 13",
            ["volume-mute"] = "M11 5 6 9H3v6h3l5 4z M17 9l5 6m0-6-5 6",
            ["fullscreen"] = "M8 3H5a2 2 0 0 0-2 2v3m13-5h3a2 2 0 0 1 2 2v3M3 16v3a2 2 0 0 0 2 2h3m13-5v3a2 2 0 0 1-2 2h-3",
            ["fullscreen-exit"] = "M8 3v5H3m13-5v5h5M3 16h5v5m13-5h-5v5",
            ["pip"] = "M3 5h18v14H3z M12 12h7v5h-7z",
            ["subtitles"] = "M3 5h18v14H3z M7 10h4m2 0h4M7 14h6m2 0h2",
            ["playlist"] = "M4 6h16M4 12h16M4 18h10 M2 6h.01M2 12h.01M2 18h.01",
            ["shuffle"] = "m16 3 4 4-4 4m-12 6 4-4m0-6 4 4m4 4 4 4-4 4M3 7h2a5 5 0 0 1 4 2l6 6a5 5 0 0 0 4 2h1M3 17h2a5 5 0 0 0 4-2l2-2m2-2 1-1a5 5 0 0 1 4-2h1",
            ["repeat"] = "M17 2l4 4-4 4 M3 11V8a2 2 0 0 1 2-2h16 M7 22l-4-4 4-4m14-1v3a2 2 0 0 1-2 2H3",
            ["refresh"] = "M20 7v5h-5M4 17v-5h5m-4-3a8 8 0 0 1 13.3-3L20 12M4 12l1.7 6a8 8 0 0 0 13.3-3",
            ["heart"] = "M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1.1-1.1a5.5 5.5 0 0 0-7.8 7.8l1.1 1.1L12 21l7.8-7.5 1.1-1.1a5.5 5.5 0 0 0-.1-7.8Z",
            ["more"] = "M5 12h.01M12 12h.01M19 12h.01",
            ["filter"] = "M4 4h16l-6 7v6l-4 3v-9z",
            ["info"] = "M12 16v-4m0-4h.01M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0Z",
            ["circle"] = "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18Z",
            ["check"] = "m5 12 4 4L19 6",
            ["trash"] = "M3 6h18M8 6V4h8v2m3 0-1 14H6L5 6m4 4v6m6-6v6",
            ["audio"] = "M4 10v4m4-7v10m4-14v18m4-14v10m4-6v2",
            ["speed"] = "M12 14l4-4m4.6 8A9 9 0 1 0 3.4 18 M12 14a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z",
            ["aspect"] = "M3 5h18v14H3z M7 9h4v3H7z M14 12h3v3h-3z",
            ["upload"] = "M12 16V4m-5 5 5-5 5 5M4 20h16",
            ["plus"] = "M12 5v14M5 12h14",
            ["arrow-left"] = "M19 12H5m7 7-7-7 7-7",
            ["external"] = "M14 3h7v7m0-7-10 10 M19 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2h6",
        };
        return paths.ToDictionary(pair => pair.Key, pair =>
        {
            var geometry = Geometry.Parse(pair.Value);
            geometry.Freeze();
            return geometry;
        }, StringComparer.OrdinalIgnoreCase);
    }
}
