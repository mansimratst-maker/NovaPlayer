using Microsoft.Data.Sqlite;
using System.IO;
using NovaPlayer.Models;
using NovaPlayer.Services;

namespace NovaPlayer.Database;

public sealed class LibraryDatabase
{
    private readonly string _connectionString;
    public LibraryDatabase()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaPlayer");
        Directory.CreateDirectory(dir);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(dir, "library.db") }.ToString();
    }
    private SqliteConnection Open() => new(_connectionString);
    public async Task InitializeAsync()
    {
        await using var db = Open(); await db.OpenAsync();
        var cmd = db.CreateCommand(); cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS Media(Path TEXT PRIMARY KEY, SizeBytes INTEGER NOT NULL, AddedAt TEXT NOT NULL, DurationMs INTEGER NOT NULL DEFAULT 0, IsFavorite INTEGER NOT NULL DEFAULT 0, LastPositionMs INTEGER NOT NULL DEFAULT 0, LastPlayedAt TEXT NULL);
            CREATE TABLE IF NOT EXISTS Folders(Path TEXT PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS Settings(Key TEXT PRIMARY KEY, Value TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Playlists(Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL COLLATE NOCASE UNIQUE, CreatedAt TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS PlaylistItems(PlaylistId INTEGER NOT NULL REFERENCES Playlists(Id) ON DELETE CASCADE, MediaPath TEXT NOT NULL, Ordinal INTEGER NOT NULL, PRIMARY KEY(PlaylistId, MediaPath));
            """; await cmd.ExecuteNonQueryAsync();
        var fk = db.CreateCommand(); fk.CommandText = "PRAGMA foreign_keys=ON;"; await fk.ExecuteNonQueryAsync();
    }
    public async Task<List<MediaItem>> GetMediaAsync(string? filter = null, string? query = null)
    {
        await using var db = Open(); await db.OpenAsync(); var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT Path,SizeBytes,AddedAt,DurationMs,IsFavorite,LastPositionMs FROM Media ORDER BY AddedAt DESC";
        var results = new List<MediaItem>(); await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var item = new MediaItem { Path = reader.GetString(0), SizeBytes = reader.GetInt64(1), AddedAt = DateTime.Parse(reader.GetString(2)).ToUniversalTime(), DurationMs = reader.GetInt64(3), IsFavorite = reader.GetInt64(4) != 0, LastPositionMs = reader.GetInt64(5) };
            if (!File.Exists(item.Path)) continue;
            if (!string.IsNullOrWhiteSpace(query) && !item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
            if (filter == "Favorites" && !item.IsFavorite) continue;
            if (filter == "Videos" && !MediaLibraryService.VideoExtensions.Contains(Path.GetExtension(item.Path))) continue;
            if (filter == "Music" && !MediaLibraryService.AudioExtensions.Contains(Path.GetExtension(item.Path))) continue;
            if (filter == "Recently Played" && item.LastPositionMs <= 0) continue;
            results.Add(item);
        }
        return results;
    }
    public async Task AddFilesAsync(IEnumerable<FileInfo> files)
    {
        await using var db = Open(); await db.OpenAsync(); await using var tx = await db.BeginTransactionAsync();
        foreach (var file in files)
        {
            var cmd = db.CreateCommand(); cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = "INSERT INTO Media(Path,SizeBytes,AddedAt) VALUES($p,$s,$d) ON CONFLICT(Path) DO UPDATE SET SizeBytes=$s";
            cmd.Parameters.AddWithValue("$p", file.FullName); cmd.Parameters.AddWithValue("$s", file.Length); cmd.Parameters.AddWithValue("$d", DateTime.UtcNow.ToString("O")); await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }
    public async Task SavePositionAsync(string path, long position, long duration)
    {
        await using var db = Open(); await db.OpenAsync(); var cmd = db.CreateCommand(); cmd.CommandText = "UPDATE Media SET LastPositionMs=$pos,DurationMs=CASE WHEN $dur>0 THEN $dur ELSE DurationMs END,LastPlayedAt=$now WHERE Path=$path";
        cmd.Parameters.AddWithValue("$pos", position); cmd.Parameters.AddWithValue("$dur", duration); cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O")); cmd.Parameters.AddWithValue("$path", path); await cmd.ExecuteNonQueryAsync();
    }
    public async Task SetFavoriteAsync(string path, bool favorite)
    {
        await using var db = Open(); await db.OpenAsync(); var cmd = db.CreateCommand(); cmd.CommandText = "UPDATE Media SET IsFavorite=$f WHERE Path=$p"; cmd.Parameters.AddWithValue("$f", favorite ? 1 : 0); cmd.Parameters.AddWithValue("$p", path); await cmd.ExecuteNonQueryAsync();
    }
    public async Task RemoveMediaAsync(string path)
    {
        await using var db = Open(); await db.OpenAsync(); await using var tx = await db.BeginTransactionAsync();
        var links = db.CreateCommand(); links.Transaction = (SqliteTransaction)tx; links.CommandText = "DELETE FROM PlaylistItems WHERE MediaPath=$path"; links.Parameters.AddWithValue("$path", path); await links.ExecuteNonQueryAsync();
        var c = db.CreateCommand(); c.Transaction = (SqliteTransaction)tx; c.CommandText = "DELETE FROM Media WHERE Path=$path"; c.Parameters.AddWithValue("$path", path); await c.ExecuteNonQueryAsync(); await tx.CommitAsync();
    }
    public async Task<List<string>> GetFoldersAsync()
    {
        await using var db = Open(); await db.OpenAsync(); var cmd = db.CreateCommand(); cmd.CommandText = "SELECT Path FROM Folders"; var list = new List<string>(); await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) list.Add(r.GetString(0)); return list;
    }
    public async Task AddFolderAsync(string path)
    {
        await using var db = Open(); await db.OpenAsync(); var c = db.CreateCommand(); c.CommandText = "INSERT OR IGNORE INTO Folders(Path) VALUES($p)"; c.Parameters.AddWithValue("$p", path); await c.ExecuteNonQueryAsync();
    }
    public async Task<string?> GetSettingAsync(string key)
    {
        await using var db = Open(); await db.OpenAsync();
        var c = db.CreateCommand(); c.CommandText = "SELECT Value FROM Settings WHERE Key=$k"; c.Parameters.AddWithValue("$k", key);
        return await c.ExecuteScalarAsync() as string;
    }
    public async Task SaveSettingAsync(string key, string value)
    {
        await using var db = Open(); await db.OpenAsync();
        var c = db.CreateCommand(); c.CommandText = "INSERT INTO Settings(Key,Value) VALUES($k,$v) ON CONFLICT(Key) DO UPDATE SET Value=$v";
        c.Parameters.AddWithValue("$k", key); c.Parameters.AddWithValue("$v", value); await c.ExecuteNonQueryAsync();
    }
    public async Task ClearSettingsAsync()
    {
        await using var db = Open(); await db.OpenAsync();
        var c = db.CreateCommand(); c.CommandText = "DELETE FROM Settings"; await c.ExecuteNonQueryAsync();
    }
    public async Task ClearHistoryAsync()
    {
        await using var db = Open(); await db.OpenAsync(); var c = db.CreateCommand(); c.CommandText = "UPDATE Media SET LastPositionMs=0, LastPlayedAt=NULL"; await c.ExecuteNonQueryAsync();
    }

    public async Task<List<SavedPlaylist>> GetPlaylistsAsync()
    {
        await using var db = Open(); await db.OpenAsync();
        var c = db.CreateCommand(); c.CommandText = "SELECT p.Id,p.Name,COUNT(i.MediaPath) FROM Playlists p LEFT JOIN PlaylistItems i ON i.PlaylistId=p.Id GROUP BY p.Id,p.Name ORDER BY p.Name COLLATE NOCASE";
        var list = new List<SavedPlaylist>(); await using var r = await c.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new SavedPlaylist { Id = r.GetInt64(0), Name = r.GetString(1), ItemCount = r.GetInt32(2) });
        return list;
    }
    public async Task<long> CreatePlaylistAsync(string name)
    {
        await using var db = Open(); await db.OpenAsync(); var c = db.CreateCommand();
        c.CommandText = "INSERT INTO Playlists(Name,CreatedAt) VALUES($name,$created); SELECT last_insert_rowid();";
        c.Parameters.AddWithValue("$name", name.Trim()); c.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
        return (long)(await c.ExecuteScalarAsync() ?? 0L);
    }
    public async Task RenamePlaylistAsync(long id, string name)
    {
        await using var db = Open(); await db.OpenAsync(); var c = db.CreateCommand(); c.CommandText = "UPDATE Playlists SET Name=$name WHERE Id=$id";
        c.Parameters.AddWithValue("$name", name.Trim()); c.Parameters.AddWithValue("$id", id); await c.ExecuteNonQueryAsync();
    }
    public async Task DeletePlaylistAsync(long id)
    {
        await using var db = Open(); await db.OpenAsync(); await using var tx = await db.BeginTransactionAsync();
        var items = db.CreateCommand(); items.Transaction = (SqliteTransaction)tx; items.CommandText = "DELETE FROM PlaylistItems WHERE PlaylistId=$id"; items.Parameters.AddWithValue("$id", id); await items.ExecuteNonQueryAsync();
        var c = db.CreateCommand(); c.Transaction = (SqliteTransaction)tx; c.CommandText = "DELETE FROM Playlists WHERE Id=$id"; c.Parameters.AddWithValue("$id", id); await c.ExecuteNonQueryAsync(); await tx.CommitAsync();
    }
    public async Task<List<MediaItem>> GetPlaylistItemsAsync(long id)
    {
        await using var db = Open(); await db.OpenAsync(); var c = db.CreateCommand();
        c.CommandText = "SELECT m.Path,m.SizeBytes,m.AddedAt,m.DurationMs,m.IsFavorite,m.LastPositionMs FROM PlaylistItems i JOIN Media m ON m.Path=i.MediaPath WHERE i.PlaylistId=$id ORDER BY i.Ordinal";
        c.Parameters.AddWithValue("$id", id); var list = new List<MediaItem>(); await using var r = await c.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var path = r.GetString(0); if (!File.Exists(path)) continue;
            list.Add(new MediaItem { Path=path, SizeBytes=r.GetInt64(1), AddedAt=DateTime.Parse(r.GetString(2)).ToUniversalTime(), DurationMs=r.GetInt64(3), IsFavorite=r.GetInt64(4)!=0, LastPositionMs=r.GetInt64(5) });
        }
        return list;
    }
    public async Task SetPlaylistItemsAsync(long id, IReadOnlyList<string> paths)
    {
        await using var db = Open(); await db.OpenAsync(); await using var tx = await db.BeginTransactionAsync();
        var del = db.CreateCommand(); del.Transaction = (SqliteTransaction)tx; del.CommandText = "DELETE FROM PlaylistItems WHERE PlaylistId=$id"; del.Parameters.AddWithValue("$id", id); await del.ExecuteNonQueryAsync();
        for (var index=0; index<paths.Count; index++)
        {
            var c = db.CreateCommand(); c.Transaction = (SqliteTransaction)tx; c.CommandText = "INSERT OR IGNORE INTO PlaylistItems(PlaylistId,MediaPath,Ordinal) VALUES($id,$path,$ordinal)";
            c.Parameters.AddWithValue("$id",id); c.Parameters.AddWithValue("$path",paths[index]); c.Parameters.AddWithValue("$ordinal",index); await c.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }
}
