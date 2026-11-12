using CommunityToolkit.Mvvm.ComponentModel;

namespace NovaPlayer.Models;

public partial class MediaItem : ObservableObject
{
    public string Path { get; set; } = "";
    public string Title => System.IO.Path.GetFileNameWithoutExtension(Path);
    public string Extension => System.IO.Path.GetExtension(Path).TrimStart('.').ToUpperInvariant();
    public bool IsAudio => System.IO.Path.GetExtension(Path).ToLowerInvariant() is ".mp3" or ".flac" or ".wav" or ".aac" or ".m4a" or ".ogg" or ".wma" or ".alac" or ".opus";
    public long SizeBytes { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public long DurationMs { get; set; }
    [ObservableProperty] private bool isFavorite;
    [ObservableProperty] private string? thumbnailPath;
    [ObservableProperty] private long lastPositionMs;
    [ObservableProperty] private bool isNowPlaying;
    public string DurationText => DurationMs > 0 ? TimeSpan.FromMilliseconds(DurationMs).ToString(DurationMs >= 3600000 ? @"h:mm:ss" : @"m:ss") : "—";
    public string SizeText => SizeBytes switch { >= 1_000_000_000 => $"{SizeBytes / 1_000_000_000d:0.0} GB", >= 1_000_000 => $"{SizeBytes / 1_000_000d:0.0} MB", _ => $"{SizeBytes / 1_000d:0} KB" };
}
