using NovaPlayer.Database;
using NovaPlayer.Models;
using System.IO;

namespace NovaPlayer.Services;
public sealed class MediaLibraryService(LibraryDatabase database) : IMediaLibraryService
{
    public static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".mpeg", ".mpg", ".ts", ".m4v", ".3gp", ".ogv", ".mts", ".m2ts" };
    public static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".flac", ".wav", ".aac", ".m4a", ".ogg", ".wma", ".alac", ".opus", ".aiff", ".ape" };
    public async Task<int> ScanFolderAsync(string folder, CancellationToken cancellationToken = default)
    {
        var files = await Task.Run(() => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Where(p => VideoExtensions.Contains(Path.GetExtension(p)) || AudioExtensions.Contains(Path.GetExtension(p))).Select(p => { try { return new FileInfo(p); } catch { return null; } }).Where(f => f is { Exists: true }).Cast<FileInfo>().ToList(), cancellationToken);
        await database.AddFilesAsync(files); await database.AddFolderAsync(folder); return files.Count;
    }
    public Task<List<MediaItem>> GetMediaAsync(string? filter = null, string? query = null) => database.GetMediaAsync(filter, query);
    public Task<List<string>> GetFoldersAsync() => database.GetFoldersAsync();
    public Task AddFolderAsync(string folder) => database.AddFolderAsync(folder);
}
