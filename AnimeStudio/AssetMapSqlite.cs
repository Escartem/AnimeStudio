using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace AnimeStudio;

public static class AssetMapSqlite
{
    public static void Save(string path, AssetMap assetMap)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();

        using SqliteTransaction transaction = connection.BeginTransaction();

        Execute
                (connection,
                 """

                 DROP TABLE IF EXISTS metadata;
                 DROP TABLE IF EXISTS asset_entries;

                 CREATE TABLE metadata (
                     key TEXT PRIMARY KEY,
                     value TEXT NOT NULL
                     );

                 CREATE TABLE asset_entries (
                     id INTEGER PRIMARY KEY,
                     name TEXT,
                     container TEXT,
                     source TEXT,
                     path_id INTEGER NOT NULL,
                     type INTEGER NOT NULL,
                     hash TEXT,
                     offset INTEGER NOT NULL
                     );
                 """);
        InsertMetadata(connection, "schema_version", "1");
        InsertMetadata(connection, "game_type", ((int) assetMap.GameType).ToString());


        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
                """
                INSERT INTO asset_entries

                (id, name, container, source, path_id, type, hash, offset)
                VALUES
                    ($id, $name, $container, $source, $path_id, $type, $hash, $offset)
                """;

        SqliteParameter id        = command.Parameters.Add("$id", SqliteType.Integer);
        SqliteParameter name      = command.Parameters.Add("$name", SqliteType.Text);
        SqliteParameter container = command.Parameters.Add("$container", SqliteType.Text);
        SqliteParameter source    = command.Parameters.Add("$source", SqliteType.Text);
        SqliteParameter pathId    = command.Parameters.Add("$path_id", SqliteType.Integer);
        SqliteParameter type      = command.Parameters.Add("$type", SqliteType.Integer);
        SqliteParameter hash      = command.Parameters.Add("$hash", SqliteType.Text);
        SqliteParameter offset    = command.Parameters.Add("$offset", SqliteType.Integer);

        for(var i = 0; i < assetMap.AssetEntries.Count; i++)
        {
            AssetEntry entry = assetMap.AssetEntries[i];
            id.Value        = i;
            name.Value      = entry.Name ?? string.Empty;
            container.Value = entry.Container ?? string.Empty;
            source.Value    = entry.Source ?? string.Empty;
            pathId.Value    = entry.PathID;
            type.Value      = (int) entry.Type;
            hash.Value      = entry.Hash ?? string.Empty;
            offset.Value    = entry.Offset;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public static AssetMap Load(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        connection.Open();

        var gameType = (GameType) int.Parse(GetMetadata(connection, "game_type"));
        var entries  = new List<AssetEntry>();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
                              SELECT name, container, source, path_id, type, hash, offset
                              FROM asset_entries
                              ORDER BY id
                              """;

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
            entries.Add
                    (new AssetEntry
                    {
                            Name      = reader.GetString(0),
                            Container = reader.GetString(1),
                            Source    = reader.GetString(2),
                            PathID    = reader.GetInt64(3),
                            Type      = (ClassIDType) reader.GetInt32(4),
                            Hash      = reader.GetString(5),
                            Offset    = reader.GetInt64(6)
                    });

        return new AssetMap
        {
                GameType     = gameType,
                AssetEntries = entries
        };
    }


    private static void Execute(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void InsertMetadata(SqliteConnection connection, string key, string value)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "INSERT INTO metadata (key, value) VALUES ($key, $value)";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private static string GetMetadata(SqliteConnection connection, string key)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM metadata WHERE key = $key";
        command.Parameters.AddWithValue("$key", key);
        return (string) command.ExecuteScalar();
    }
}