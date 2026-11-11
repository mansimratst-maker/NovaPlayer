using NovaPlayer.Models;

namespace NovaPlayer.Services;
public interface IMediaLibraryService
{
    Task<int> ScanFolderAsync(string folder, CancellationToken cancellationToken = default);
    Task<List<MediaItem>> GetMediaAsync(string? filter = null, string? query = null);
    Task<List<string>> GetFoldersAsync();
    Task AddFolderAsync(string folder);
}
