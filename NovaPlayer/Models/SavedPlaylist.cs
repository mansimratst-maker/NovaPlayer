namespace NovaPlayer.Models;

public sealed class SavedPlaylist
{
    public long Id { get; init; }
    public string Name { get; set; } = "";
    public int ItemCount { get; init; }
}
