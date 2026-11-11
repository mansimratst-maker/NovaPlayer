using NovaPlayer.Models;

namespace NovaPlayer.Services;

public interface IMediaThumbnailService
{
    Task<string?> GetThumbnailAsync(MediaItem item, CancellationToken cancellationToken = default);
}
