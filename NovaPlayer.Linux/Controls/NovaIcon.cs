using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NovaPlayer.Linux.Controls;

/// <summary>A small DPI-crisp Lucide-style vector catalog shared by the Linux UI.</summary>
public sealed class NovaIcon : Control
{
    public static readonly StyledProperty<string> KindProperty = AvaloniaProperty.Register<NovaIcon, string>(nameof(Kind), "circle");
    public static readonly StyledProperty<double> IconSizeProperty = AvaloniaProperty.Register<NovaIcon, double>(nameof(IconSize), 24);
    public static readonly StyledProperty<string> StrokeProperty = AvaloniaProperty.Register<NovaIcon, string>(nameof(Stroke), "#F4F5FF");
    private static readonly IReadOnlyDictionary<string, Geometry> Icons = BuildIcons();

    public string Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public double IconSize { get => GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }
    public string Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(IconSize, IconSize);
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (!Icons.TryGetValue(Kind, out var path)) path = Icons["circle"];
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 24d;
        if (scale <= 0) return;
        var brush = new SolidColorBrush(Color.Parse(Stroke));
        var pen = new Pen(brush, 1.8 / scale, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        using var transform = context.PushTransform(Matrix.CreateScale(new Vector(scale, scale)));
        context.DrawGeometry(null, pen, path);
    }

    private static IReadOnlyDictionary<string, Geometry> BuildIcons()
    {
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["home"]="M3 10.5 12 3l9 7.5 M5 9v11h5v-6h4v6h5V9",
            ["library"]="M4 4h16v16H4z M8 8h8M8 12h8M8 16h5",
            ["video"]="M3 7h13v10H3zM16 10l5-3v10l-5-3z",
            ["music"]="M9 18V5l12-2v13 M9 18a3 3 0 1 1-3-3 3 3 0 0 1 3 3Zm12-2a3 3 0 1 1-3-3 3 3 0 0 1 3 3Z",
            ["history"]="M3 12a9 9 0 1 0 2.64-6.36L3 8 M3 3v5h5 M12 7v5l3 2",
            ["heart"]="M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1.1-1.1a5.5 5.5 0 0 0-7.8 7.8l1.1 1.1L12 21l7.8-7.5 1.1-1.1a5.5 5.5 0 0 0-.1-7.8Z",
            ["settings"]="M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8Zm0-5v2m0 14v2m9-9h-2M5 12H3m15.36-6.36-1.42 1.42M7.06 16.94l-1.42 1.42m12.72 0-1.42-1.42M7.06 7.06 5.64 5.64",
            ["playlist"]="M4 6h16M4 12h16M4 18h10M2 6h.01M2 12h.01M2 18h.01",
            ["plus"]="M12 5v14M5 12h14",
            ["play"]="M8 5 19 12 8 19z",
            ["pause"]="M8 5v14M16 5v14",
            ["previous"]="M19 5v14M17 12 5 5v14z",
            ["next"]="M5 5v14M7 12l12-7v14z",
            ["rewind"]="m11 19-9-7 9-7v14Zm10 0-9-7 9-7v14Z",
            ["forward"]="m13 5 9 7-9 7V5ZM3 5l9 7-9 7V5Z",
            ["volume"]="M11 5 6 9H3v6h3l5 4zM15.5 8.5a5 5 0 0 1 0 7m3-10a9 9 0 0 1 0 13",
            ["subtitles"]="M3 5h18v14H3zM7 10h4m2 0h4M7 14h6m2 0h2",
            ["pip"]="M3 5h18v14H3zM12 12h7v5h-7z",
            ["fullscreen"]="M8 3H5a2 2 0 0 0-2 2v3m13-5h3a2 2 0 0 1 2 2v3M3 16v3a2 2 0 0 0 2 2h3m13-5v3a2 2 0 0 1-2 2h-3",
            ["shuffle"]="m16 3 4 4-4 4m-12 6 4-4m0-6 4 4m4 4 4 4-4 4M3 7h2a5 5 0 0 1 4 2l6 6a5 5 0 0 0 4 2h1M3 17h2a5 5 0 0 0 4-2l2-2m2-2 1-1a5 5 0 0 1 4-2h1",
            ["repeat"]="M17 2l4 4-4 4M3 11V8a2 2 0 0 1 2-2h16M7 22l-4-4 4-4m14-1v3a2 2 0 0 1-2 2H3",
            ["refresh"]="M20 7v5h-5M4 17v-5h5m-4-3a8 8 0 0 1 13.3-3L20 12M4 12l1.7 6a8 8 0 0 0 13.3-3",
            ["folder"]="M3 6a2 2 0 0 1 2-2h5l2 2h7a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z",
            ["close"]="m18 6-12 12M6 6l12 12",
            ["circle"]="M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18Z"
        };
        return paths.ToDictionary(p => p.Key, p => Geometry.Parse(p.Value), StringComparer.OrdinalIgnoreCase);
    }
}
