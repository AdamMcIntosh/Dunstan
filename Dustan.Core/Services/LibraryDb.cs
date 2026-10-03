using Dustan.Models;
using Microsoft.Data.Sqlite;

namespace Dustan.Services;

public sealed class LibraryDb : IDisposable
{
    private readonly string _connectionString;
    private readonly object _lock = new();
    private SqliteConnection? _connection;

    public LibraryDb(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();
    }

    public void Initialize()
    {
        lock (_lock)
        {
            var conn = OpenConnectionUnlocked();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                PRAGMA journal_mode=WAL;
                PRAGMA foreign_keys=ON;

                CREATE TABLE IF NOT EXISTS artists (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS idx_artists_name_unique
                    ON artists(name COLLATE NOCASE);

                CREATE TABLE IF NOT EXISTS albums (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    album_artist TEXT NOT NULL,
                    year INTEGER NOT NULL DEFAULT 0,
                    artwork_path TEXT NULL,
                    folder_path TEXT NOT NULL DEFAULT ''
                );

                CREATE TABLE IF NOT EXISTS tracks (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    path TEXT NOT NULL,
                    file_size INTEGER NOT NULL,
                    modified_utc_ticks INTEGER NOT NULL,
                    title TEXT NOT NULL,
                    artist TEXT NOT NULL,
                    album TEXT NOT NULL,
                    album_artist TEXT NOT NULL,
                    track_number INTEGER NOT NULL DEFAULT 0,
                    disc_number INTEGER NOT NULL DEFAULT 0,
                    year INTEGER NOT NULL DEFAULT 0,
                    genre TEXT NOT NULL DEFAULT '',
                    duration_seconds REAL NOT NULL DEFAULT 0,
                    album_id INTEGER NULL REFERENCES albums(id) ON DELETE SET NULL,
                    artist_id INTEGER NULL REFERENCES artists(id) ON DELETE SET NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS idx_tracks_path_unique
                    ON tracks(path COLLATE NOCASE);

                CREATE INDEX IF NOT EXISTS idx_tracks_album_id ON tracks(album_id);
                CREATE INDEX IF NOT EXISTS idx_tracks_artist_id ON tracks(artist_id);
                CREATE INDEX IF NOT EXISTS idx_tracks_title ON tracks(title COLLATE NOCASE);
                CREATE INDEX IF NOT EXISTS idx_albums_name ON albums(name COLLATE NOCASE);
                CREATE INDEX IF NOT EXISTS idx_artists_name ON artists(name COLLATE NOCASE);
                """;
            cmd.ExecuteNonQuery();

            EnsureAlbumFolderColumnUnlocked(conn);
            RebuildAlbumFoldersUnlocked(conn);
        }
    }

    private static void EnsureAlbumFolderColumnUnlocked(SqliteConnection conn)
    {
        using (var info = conn.CreateCommand())
        {
            info.CommandText = "PRAGMA table_info(albums);";
            var hasFolder = false;
            using var reader = info.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), "folder_path", StringComparison.OrdinalIgnoreCase))
                {
                    hasFolder = true;
                    break;
                }
            }

            if (!hasFolder)
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE albums ADD COLUMN folder_path TEXT NOT NULL DEFAULT '';";
                alter.ExecuteNonQuery();
            }
        }

        using var drop = conn.CreateCommand();
        drop.CommandText = "DROP INDEX IF EXISTS idx_albums_unique;";
        drop.ExecuteNonQuery();

        using var create = conn.CreateCommand();
        create.CommandText =
            """
            CREATE UNIQUE INDEX IF NOT EXISTS idx_albums_unique
                ON albums(name COLLATE NOCASE, album_artist COLLATE NOCASE, folder_path COLLATE NOCASE);
            """;
        create.ExecuteNonQuery();
    }

    /// <summary>
    /// Reassigns every track to an album keyed by (name, album artist, folder)
    /// so duplicate NAS copies become separate albums.
    /// </summary>
    public void RebuildAlbumFolders()
    {
        lock (_lock)
        {
            RebuildAlbumFoldersUnlocked(OpenConnectionUnlocked());
        }
    }

    private void RebuildAlbumFoldersUnlocked(SqliteConnection conn)
    {
        using var list = conn.CreateCommand();
        list.CommandText =
            """
            SELECT t.id, t.path, t.album, t.album_artist, t.year, a.artwork_path
            FROM tracks t
            LEFT JOIN albums a ON a.id = t.album_id;
            """;

        var rows = new List<(long Id, string Path, string Album, string AlbumArtist, int Year, string? Art)>();
        using (var reader = list.ExecuteReader())
        {
            while (reader.Read())
            {
                rows.Add((
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetInt32(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5)));
            }
        }

        foreach (var row in rows)
        {
            var folder = ArtworkCache.NormalizeFolder(Path.GetDirectoryName(row.Path));
            var albumId = UpsertAlbumUnlocked(conn, row.Album, row.AlbumArtist, row.Year, row.Art, folder);

            using var update = conn.CreateCommand();
            update.CommandText = "UPDATE tracks SET album_id=$album_id WHERE id=$id;";
            update.Parameters.AddWithValue("$album_id", albumId);
            update.Parameters.AddWithValue("$id", row.Id);
            update.ExecuteNonQuery();
        }

        using var cleanup = conn.CreateCommand();
        cleanup.CommandText =
            """
            DELETE FROM albums WHERE id NOT IN (SELECT DISTINCT album_id FROM tracks WHERE album_id IS NOT NULL);
            DELETE FROM artists WHERE id NOT IN (SELECT DISTINCT artist_id FROM tracks WHERE artist_id IS NOT NULL);
            """;
        cleanup.ExecuteNonQuery();
    }

    public SqliteConnection GetConnection()
    {
        lock (_lock)
        {
            return OpenConnectionUnlocked();
        }
    }

    private SqliteConnection OpenConnectionUnlocked()
    {
        if (_connection is { State: System.Data.ConnectionState.Open })
        {
            return _connection;
        }

        _connection?.Dispose();
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
        return _connection;
    }

    public Dictionary<string, (long Id, long Size, long Mtime)> GetTrackFingerprints()
    {
        var result = new Dictionary<string, (long, long, long)>(PathSafety.Comparer);
        using var cmd = GetConnection().CreateCommand();
        cmd.CommandText = "SELECT id, path, file_size, modified_utc_ticks FROM tracks;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result[reader.GetString(1)] = (reader.GetInt64(0), reader.GetInt64(2), reader.GetInt64(3));
        }

        return result;
    }

    public long UpsertArtist(string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Unknown Artist" : name.Trim();
        using var find = GetConnection().CreateCommand();
        find.CommandText = "SELECT id FROM artists WHERE name = $name COLLATE NOCASE LIMIT 1;";
        find.Parameters.AddWithValue("$name", name);
        var existing = find.ExecuteScalar();
        if (existing is long id)
        {
            return id;
        }

        using var insert = GetConnection().CreateCommand();
        insert.CommandText = "INSERT INTO artists(name) VALUES($name); SELECT last_insert_rowid();";
        insert.Parameters.AddWithValue("$name", name);
        return (long)insert.ExecuteScalar()!;
    }

    public long UpsertAlbum(string name, string albumArtist, int year, string? artworkPath, string? folderPath)
    {
        return UpsertAlbumUnlocked(GetConnection(), name, albumArtist, year, artworkPath, folderPath);
    }

    private static long UpsertAlbumUnlocked(
        SqliteConnection conn,
        string name,
        string albumArtist,
        int year,
        string? artworkPath,
        string? folderPath)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Unknown Album" : name.Trim();
        albumArtist = string.IsNullOrWhiteSpace(albumArtist) ? "Unknown Artist" : albumArtist.Trim();
        var folder = ArtworkCache.NormalizeFolder(folderPath);

        using var find = conn.CreateCommand();
        find.CommandText =
            """
            SELECT id, year, artwork_path FROM albums
            WHERE name = $name COLLATE NOCASE
              AND album_artist = $album_artist COLLATE NOCASE
              AND folder_path = $folder COLLATE NOCASE
            LIMIT 1;
            """;
        find.Parameters.AddWithValue("$name", name);
        find.Parameters.AddWithValue("$album_artist", albumArtist);
        find.Parameters.AddWithValue("$folder", folder);
        using (var reader = find.ExecuteReader())
        {
            if (reader.Read())
            {
                var id = reader.GetInt64(0);
                var currentYear = reader.GetInt32(1);
                var currentArt = reader.IsDBNull(2) ? null : reader.GetString(2);
                reader.Close();

                if ((year > 0 && year != currentYear) || (artworkPath is not null && artworkPath != currentArt))
                {
                    using var update = conn.CreateCommand();
                    update.CommandText =
                        """
                        UPDATE albums SET
                            year = CASE WHEN $year > 0 THEN $year ELSE year END,
                            artwork_path = COALESCE($artwork, artwork_path)
                        WHERE id = $id;
                        """;
                    update.Parameters.AddWithValue("$year", year);
                    update.Parameters.AddWithValue("$artwork", (object?)artworkPath ?? DBNull.Value);
                    update.Parameters.AddWithValue("$id", id);
                    update.ExecuteNonQuery();
                }

                return id;
            }
        }

        using var insert = conn.CreateCommand();
        insert.CommandText =
            """
            INSERT INTO albums(name, album_artist, year, artwork_path, folder_path)
            VALUES($name, $album_artist, $year, $artwork, $folder);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$name", name);
        insert.Parameters.AddWithValue("$album_artist", albumArtist);
        insert.Parameters.AddWithValue("$year", year);
        insert.Parameters.AddWithValue("$artwork", (object?)artworkPath ?? DBNull.Value);
        insert.Parameters.AddWithValue("$folder", folder);
        return (long)insert.ExecuteScalar()!;
    }

    /// <summary>
    /// Renames an existing album row in place within its folder. Does not merge
    /// with same-named albums in other folders.
    /// </summary>
    public long UpdateAlbumIdentity(long albumId, string name, string albumArtist, int year, string? artworkPath)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Unknown Album" : name.Trim();
        albumArtist = string.IsNullOrWhiteSpace(albumArtist) ? "Unknown Artist" : albumArtist.Trim();

        string folder;
        using (var folderCmd = GetConnection().CreateCommand())
        {
            folderCmd.CommandText = "SELECT folder_path FROM albums WHERE id=$id;";
            folderCmd.Parameters.AddWithValue("$id", albumId);
            folder = ArtworkCache.NormalizeFolder(folderCmd.ExecuteScalar() as string);
        }

        // Merge only into another row that shares this folder (same physical copy).
        using var find = GetConnection().CreateCommand();
        find.CommandText =
            """
            SELECT id FROM albums
            WHERE name = $name COLLATE NOCASE
              AND album_artist = $album_artist COLLATE NOCASE
              AND folder_path = $folder COLLATE NOCASE
            LIMIT 1;
            """;
        find.Parameters.AddWithValue("$name", name);
        find.Parameters.AddWithValue("$album_artist", albumArtist);
        find.Parameters.AddWithValue("$folder", folder);
        var existing = find.ExecuteScalar();
        if (existing is long otherId && otherId != albumId)
        {
            using var move = GetConnection().CreateCommand();
            move.CommandText = "UPDATE tracks SET album_id=$to WHERE album_id=$from;";
            move.Parameters.AddWithValue("$to", otherId);
            move.Parameters.AddWithValue("$from", albumId);
            move.ExecuteNonQuery();

            using var delete = GetConnection().CreateCommand();
            delete.CommandText = "DELETE FROM albums WHERE id=$id;";
            delete.Parameters.AddWithValue("$id", albumId);
            delete.ExecuteNonQuery();

            using var touch = GetConnection().CreateCommand();
            touch.CommandText =
                """
                UPDATE albums SET
                    year = CASE WHEN $year > 0 THEN $year ELSE year END,
                    artwork_path = $artwork
                WHERE id = $id;
                """;
            touch.Parameters.AddWithValue("$year", year);
            touch.Parameters.AddWithValue("$artwork", (object?)artworkPath ?? DBNull.Value);
            touch.Parameters.AddWithValue("$id", otherId);
            touch.ExecuteNonQuery();
            return otherId;
        }

        using var update = GetConnection().CreateCommand();
        update.CommandText =
            """
            UPDATE albums SET
                name = $name,
                album_artist = $album_artist,
                year = CASE WHEN $year > 0 THEN $year ELSE year END,
                artwork_path = $artwork
            WHERE id = $id;
            """;
        update.Parameters.AddWithValue("$name", name);
        update.Parameters.AddWithValue("$album_artist", albumArtist);
        update.Parameters.AddWithValue("$year", year);
        update.Parameters.AddWithValue("$artwork", (object?)artworkPath ?? DBNull.Value);
        update.Parameters.AddWithValue("$id", albumId);
        update.ExecuteNonQuery();
        return albumId;
    }

    public void UpsertTrack(TrackInfo track, long artistId, long albumId)
    {
        using var find = GetConnection().CreateCommand();
        find.CommandText = "SELECT id FROM tracks WHERE path = $path COLLATE NOCASE LIMIT 1;";
        find.Parameters.AddWithValue("$path", track.Path);
        var existing = find.ExecuteScalar();

        if (existing is long id)
        {
            using var update = GetConnection().CreateCommand();
            update.CommandText =
                """
                UPDATE tracks SET
                    file_size=$size,
                    modified_utc_ticks=$mtime,
                    title=$title,
                    artist=$artist,
                    album=$album,
                    album_artist=$album_artist,
                    track_number=$track_number,
                    disc_number=$disc_number,
                    year=$year,
                    genre=$genre,
                    duration_seconds=$duration,
                    album_id=$album_id,
                    artist_id=$artist_id
                WHERE id=$id;
                """;
            update.Parameters.AddWithValue("$size", track.FileSize);
            update.Parameters.AddWithValue("$mtime", track.ModifiedUtcTicks);
            update.Parameters.AddWithValue("$title", track.Title);
            update.Parameters.AddWithValue("$artist", track.Artist);
            update.Parameters.AddWithValue("$album", track.Album);
            update.Parameters.AddWithValue("$album_artist", track.AlbumArtist);
            update.Parameters.AddWithValue("$track_number", track.TrackNumber);
            update.Parameters.AddWithValue("$disc_number", track.DiscNumber);
            update.Parameters.AddWithValue("$year", track.Year);
            update.Parameters.AddWithValue("$genre", track.Genre);
            update.Parameters.AddWithValue("$duration", track.DurationSeconds);
            update.Parameters.AddWithValue("$album_id", albumId);
            update.Parameters.AddWithValue("$artist_id", artistId);
            update.Parameters.AddWithValue("$id", id);
            update.ExecuteNonQuery();
            return;
        }

        using var insert = GetConnection().CreateCommand();
        insert.CommandText =
            """
            INSERT INTO tracks(
                path, file_size, modified_utc_ticks, title, artist, album, album_artist,
                track_number, disc_number, year, genre, duration_seconds, album_id, artist_id)
            VALUES(
                $path, $size, $mtime, $title, $artist, $album, $album_artist,
                $track_number, $disc_number, $year, $genre, $duration, $album_id, $artist_id);
            """;
        insert.Parameters.AddWithValue("$path", track.Path);
        insert.Parameters.AddWithValue("$size", track.FileSize);
        insert.Parameters.AddWithValue("$mtime", track.ModifiedUtcTicks);
        insert.Parameters.AddWithValue("$title", track.Title);
        insert.Parameters.AddWithValue("$artist", track.Artist);
        insert.Parameters.AddWithValue("$album", track.Album);
        insert.Parameters.AddWithValue("$album_artist", track.AlbumArtist);
        insert.Parameters.AddWithValue("$track_number", track.TrackNumber);
        insert.Parameters.AddWithValue("$disc_number", track.DiscNumber);
        insert.Parameters.AddWithValue("$year", track.Year);
        insert.Parameters.AddWithValue("$genre", track.Genre);
        insert.Parameters.AddWithValue("$duration", track.DurationSeconds);
        insert.Parameters.AddWithValue("$album_id", albumId);
        insert.Parameters.AddWithValue("$artist_id", artistId);
        insert.ExecuteNonQuery();
    }

    public int DeleteTracksByIds(IEnumerable<long> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
        {
            return 0;
        }

        using var transaction = GetConnection().BeginTransaction();
        using var cmd = GetConnection().CreateCommand();
        cmd.Transaction = transaction;
        var removed = 0;
        foreach (var id in idList)
        {
            cmd.Parameters.Clear();
            cmd.CommandText = "DELETE FROM tracks WHERE id=$id;";
            cmd.Parameters.AddWithValue("$id", id);
            removed += cmd.ExecuteNonQuery();
        }

        transaction.Commit();
        return removed;
    }

    public void CleanupOrphans()
    {
        using var cmd = GetConnection().CreateCommand();
        cmd.CommandText =
            """
            DELETE FROM albums WHERE id NOT IN (SELECT DISTINCT album_id FROM tracks WHERE album_id IS NOT NULL);
            DELETE FROM artists WHERE id NOT IN (SELECT DISTINCT artist_id FROM tracks WHERE artist_id IS NOT NULL);
            """;
        cmd.ExecuteNonQuery();
    }

    public void SetAlbumArtwork(long albumId, string? artworkPath)
    {
        using var cmd = GetConnection().CreateCommand();
        cmd.CommandText = "UPDATE albums SET artwork_path=$path WHERE id=$id;";
        cmd.Parameters.AddWithValue("$path", (object?)artworkPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", albumId);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<AlbumInfo> GetAlbums(string? search = null)
    {
        using var cmd = GetConnection().CreateCommand();
        if (string.IsNullOrWhiteSpace(search))
        {
            cmd.CommandText =
                """
                SELECT a.id, a.name, a.album_artist, a.year, a.artwork_path,
                       (SELECT COUNT(*) FROM tracks t WHERE t.album_id = a.id) AS track_count
                FROM albums a
                ORDER BY a.album_artist COLLATE NOCASE, a.name COLLATE NOCASE, a.folder_path COLLATE NOCASE;
                """;
        }
        else
        {
            cmd.CommandText =
                """
                SELECT a.id, a.name, a.album_artist, a.year, a.artwork_path,
                       (SELECT COUNT(*) FROM tracks t WHERE t.album_id = a.id) AS track_count
                FROM albums a
                WHERE a.name LIKE $q OR a.album_artist LIKE $q
                ORDER BY a.album_artist COLLATE NOCASE, a.name COLLATE NOCASE, a.folder_path COLLATE NOCASE;
                """;
            cmd.Parameters.AddWithValue("$q", $"%{search.Trim()}%");
        }

        return ReadAlbums(cmd);
    }

    public AlbumInfo? GetAlbum(long id)
    {
        using var cmd = GetConnection().CreateCommand();
        cmd.CommandText =
            """
            SELECT a.id, a.name, a.album_artist, a.year, a.artwork_path,
                   (SELECT COUNT(*) FROM tracks t WHERE t.album_id = a.id) AS track_count
            FROM albums a WHERE a.id=$id;
            """;
        cmd.Parameters.AddWithValue("$id", id);
        return ReadAlbums(cmd).FirstOrDefault();
    }

    public IReadOnlyList<ArtistInfo> GetArtists(string? search = null)
    {
        using var cmd = GetConnection().CreateCommand();
        if (string.IsNullOrWhiteSpace(search))
        {
            cmd.CommandText =
                """
                SELECT ar.id, ar.name,
                       (
                         SELECT COUNT(*) FROM albums a
                         WHERE a.album_artist = ar.name COLLATE NOCASE
                            OR EXISTS (
                                 SELECT 1 FROM tracks t
                                 WHERE t.album_id = a.id AND t.artist_id = ar.id
                            )
                       ) AS album_count,
                       (
                         SELECT COUNT(*) FROM tracks t
                         LEFT JOIN albums a ON a.id = t.album_id
                         WHERE t.artist_id = ar.id
                            OR a.album_artist = ar.name COLLATE NOCASE
                       ) AS track_count
                FROM artists ar
                ORDER BY ar.name COLLATE NOCASE;
                """;
        }
        else
        {
            cmd.CommandText =
                """
                SELECT ar.id, ar.name,
                       (
                         SELECT COUNT(*) FROM albums a
                         WHERE a.album_artist = ar.name COLLATE NOCASE
                            OR EXISTS (
                                 SELECT 1 FROM tracks t
                                 WHERE t.album_id = a.id AND t.artist_id = ar.id
                            )
                       ) AS album_count,
                       (
                         SELECT COUNT(*) FROM tracks t
                         LEFT JOIN albums a ON a.id = t.album_id
                         WHERE t.artist_id = ar.id
                            OR a.album_artist = ar.name COLLATE NOCASE
                       ) AS track_count
                FROM artists ar
                WHERE ar.name LIKE $q
                ORDER BY ar.name COLLATE NOCASE;
                """;
            cmd.Parameters.AddWithValue("$q", $"%{search.Trim()}%");
        }

        var list = new List<ArtistInfo>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new ArtistInfo
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                AlbumCount = reader.GetInt32(2),
                TrackCount = reader.GetInt32(3)
            });
        }

        return list;
    }

    public ArtistInfo? GetArtist(long id)
    {
        using var cmd = GetConnection().CreateCommand();
        cmd.CommandText =
            """
            SELECT ar.id, ar.name,
                   (
                     SELECT COUNT(*) FROM albums a
                     WHERE a.album_artist = ar.name COLLATE NOCASE
                        OR EXISTS (
                             SELECT 1 FROM tracks t
                             WHERE t.album_id = a.id AND t.artist_id = ar.id
                        )
                   ) AS album_count,
                   (
                     SELECT COUNT(*) FROM tracks t
                     LEFT JOIN albums a ON a.id = t.album_id
                     WHERE t.artist_id = ar.id
                        OR a.album_artist = ar.name COLLATE NOCASE
                   ) AS track_count
            FROM artists ar WHERE ar.id=$id;
            """;
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new ArtistInfo
        {
            Id = reader.GetInt64(0),
            Name = reader.GetString(1),
            AlbumCount = reader.GetInt32(2),
            TrackCount = reader.GetInt32(3)
        };
    }

    public IReadOnlyList<AlbumInfo> GetAlbumsForArtist(long artistId)
    {
        using var cmd = GetConnection().CreateCommand();
        // Album-artist owns the discography; also include albums that contain
        // this artist's tracks (compilations / mismatched tags).
        cmd.CommandText =
            """
            SELECT DISTINCT a.id, a.name, a.album_artist, a.year, a.artwork_path,
                   (SELECT COUNT(*) FROM tracks t2 WHERE t2.album_id = a.id) AS track_count
            FROM albums a
            INNER JOIN artists ar ON ar.id = $id
            WHERE a.album_artist = ar.name COLLATE NOCASE
               OR EXISTS (
                    SELECT 1 FROM tracks t
                    WHERE t.album_id = a.id AND t.artist_id = $id
               )
            ORDER BY a.year, a.name COLLATE NOCASE, a.folder_path COLLATE NOCASE;
            """;
        cmd.Parameters.AddWithValue("$id", artistId);
        return ReadAlbums(cmd);
    }

    public ArtistInfo? GetArtistByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        using var cmd = GetConnection().CreateCommand();
        cmd.CommandText =
            """
            SELECT ar.id, ar.name,
                   (
                     SELECT COUNT(*) FROM albums a
                     WHERE a.album_artist = ar.name COLLATE NOCASE
                        OR EXISTS (
                             SELECT 1 FROM tracks t
                             WHERE t.album_id = a.id AND t.artist_id = ar.id
                        )
                   ) AS album_count,
                   (
                     SELECT COUNT(*) FROM tracks t
                     LEFT JOIN albums a ON a.id = t.album_id
                     WHERE t.artist_id = ar.id
                        OR a.album_artist = ar.name COLLATE NOCASE
                   ) AS track_count
            FROM artists ar
            WHERE ar.name = $name COLLATE NOCASE
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$name", name.Trim());
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new ArtistInfo
        {
            Id = reader.GetInt64(0),
            Name = reader.GetString(1),
            AlbumCount = reader.GetInt32(2),
            TrackCount = reader.GetInt32(3)
        };
    }

    public IReadOnlyList<TrackInfo> GetTracks(string? search = null)
    {
        using var cmd = GetConnection().CreateCommand();
        if (string.IsNullOrWhiteSpace(search))
        {
            cmd.CommandText =
                """
                SELECT id, path, file_size, modified_utc_ticks, title, artist, album, album_artist,
                       track_number, disc_number, year, genre, duration_seconds, album_id, artist_id
                FROM tracks
                ORDER BY artist COLLATE NOCASE, album COLLATE NOCASE, disc_number, track_number, title COLLATE NOCASE;
                """;
        }
        else
        {
            cmd.CommandText =
                """
                SELECT id, path, file_size, modified_utc_ticks, title, artist, album, album_artist,
                       track_number, disc_number, year, genre, duration_seconds, album_id, artist_id
                FROM tracks
                WHERE title LIKE $q OR artist LIKE $q OR album LIKE $q
                ORDER BY artist COLLATE NOCASE, album COLLATE NOCASE, disc_number, track_number, title COLLATE NOCASE;
                """;
            cmd.Parameters.AddWithValue("$q", $"%{search.Trim()}%");
        }

        return ReadTracks(cmd);
    }

    public IReadOnlyList<TrackInfo> GetTracksForAlbum(long albumId)
    {
        using var cmd = GetConnection().CreateCommand();
        cmd.CommandText =
            """
            SELECT id, path, file_size, modified_utc_ticks, title, artist, album, album_artist,
                   track_number, disc_number, year, genre, duration_seconds, album_id, artist_id
            FROM tracks
            WHERE album_id=$id
            ORDER BY disc_number, track_number, title COLLATE NOCASE;
            """;
        cmd.Parameters.AddWithValue("$id", albumId);
        return ReadTracks(cmd);
    }

    public IReadOnlyList<TrackInfo> GetTracksForArtist(long artistId)
    {
        using var cmd = GetConnection().CreateCommand();
        cmd.CommandText =
            """
            SELECT DISTINCT t.id, t.path, t.file_size, t.modified_utc_ticks, t.title, t.artist, t.album, t.album_artist,
                   t.track_number, t.disc_number, t.year, t.genre, t.duration_seconds, t.album_id, t.artist_id
            FROM tracks t
            INNER JOIN artists ar ON ar.id = $id
            LEFT JOIN albums a ON a.id = t.album_id
            WHERE t.artist_id = $id
               OR a.album_artist = ar.name COLLATE NOCASE
            ORDER BY t.album COLLATE NOCASE, t.disc_number, t.track_number, t.title COLLATE NOCASE;
            """;
        cmd.Parameters.AddWithValue("$id", artistId);
        return ReadTracks(cmd);
    }

    public int GetTrackCount()
    {
        using var cmd = GetConnection().CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM tracks;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static List<AlbumInfo> ReadAlbums(SqliteCommand cmd)
    {
        var list = new List<AlbumInfo>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new AlbumInfo
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                AlbumArtist = reader.GetString(2),
                Year = reader.GetInt32(3),
                ArtworkPath = reader.IsDBNull(4) ? null : reader.GetString(4),
                TrackCount = reader.GetInt32(5)
            });
        }

        return list;
    }

    private static List<TrackInfo> ReadTracks(SqliteCommand cmd)
    {
        var list = new List<TrackInfo>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new TrackInfo
            {
                Id = reader.GetInt64(0),
                Path = reader.GetString(1),
                FileSize = reader.GetInt64(2),
                ModifiedUtcTicks = reader.GetInt64(3),
                Title = reader.GetString(4),
                Artist = reader.GetString(5),
                Album = reader.GetString(6),
                AlbumArtist = reader.GetString(7),
                TrackNumber = reader.GetInt32(8),
                DiscNumber = reader.GetInt32(9),
                Year = reader.GetInt32(10),
                Genre = reader.GetString(11),
                DurationSeconds = reader.GetDouble(12),
                AlbumId = reader.IsDBNull(13) ? null : reader.GetInt64(13),
                ArtistId = reader.IsDBNull(14) ? null : reader.GetInt64(14)
            });
        }

        return list;
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
