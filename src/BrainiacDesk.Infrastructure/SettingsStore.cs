using BrainiacDesk.Core;
using Microsoft.Data.Sqlite;
using System.Text.Json;
namespace BrainiacDesk.Infrastructure;
public sealed class SettingsStore(string path) : ISettingsStore
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private async Task<SqliteConnection> OpenAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
        try
        {
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE IF NOT EXISTS settings (id INTEGER PRIMARY KEY CHECK(id=1), json TEXT NOT NULL)";
            await command.ExecuteNonQueryAsync();
            return connection;
        }
        catch { await connection.DisposeAsync(); throw; }
    }
    public async Task<UserSettings> LoadAsync()
    {
        await gate.WaitAsync();
        try
        {
            await using var connection = await OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT json FROM settings WHERE id=1";
            var value = await command.ExecuteScalarAsync() as string;
            return value is null ? new() : JsonSerializer.Deserialize<UserSettings>(value) ?? throw new InvalidDataException("Settings could not be read.");
        }
        finally { gate.Release(); }
    }
    public async Task SaveAsync(UserSettings settings)
    {
        await gate.WaitAsync();
        try
        {
            await using var connection = await OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO settings(id,json) VALUES(1,$json) ON CONFLICT(id) DO UPDATE SET json=excluded.json";
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(settings));
            await command.ExecuteNonQueryAsync();
        }
        finally { gate.Release(); }
    }
}