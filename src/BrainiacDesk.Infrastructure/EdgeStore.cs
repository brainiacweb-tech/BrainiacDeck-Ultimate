using System.Text.Json;
using BrainiacDesk.Core;
using Microsoft.Data.Sqlite;
namespace BrainiacDesk.Infrastructure;
/// <summary>Additive Edge tables in the application's existing settings database.</summary>
public sealed class EdgeStore(string path) : IEdgeStore
{
    private bool initialized;
    private readonly SemaphoreSlim gate = new(1, 1);
    private async Task<T> With<T>(Func<SqliteConnection, Task<T>> work)
    {
        await gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            await using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
            await db.OpenAsync();
            if(!initialized)
            {
                using var versionCheck=db.CreateCommand(); versionCheck.CommandText="SELECT count(*) FROM sqlite_master WHERE type='table' AND name='edge_schema'";
                if(Convert.ToInt32(await versionCheck.ExecuteScalarAsync())>0) { versionCheck.CommandText="SELECT coalesce(max(version),0) FROM edge_schema"; if(Convert.ToInt32(await versionCheck.ExecuteScalarAsync())>2) throw new InvalidDataException("This Edge database was written by a newer app. It was preserved without modification."); }
                using var migration=db.BeginTransaction();
                using var schema = db.CreateCommand(); schema.Transaction=migration;
                schema.CommandText = """
                CREATE TABLE IF NOT EXISTS edge_items(id TEXT PRIMARY KEY, workspace TEXT NOT NULL, fingerprint TEXT NOT NULL, json TEXT NOT NULL, UNIQUE(workspace,fingerprint));
                CREATE TABLE IF NOT EXISTS edge_settings(id INTEGER PRIMARY KEY CHECK(id=1), json TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS edge_schema(version INTEGER PRIMARY KEY);
                INSERT OR IGNORE INTO edge_schema VALUES(1);
                CREATE VIRTUAL TABLE IF NOT EXISTS edge_fts USING fts5(id UNINDEXED,title,content,tags,ocr,tokenize='unicode61');
                CREATE TRIGGER IF NOT EXISTS edge_search_insert AFTER INSERT ON edge_items BEGIN
                  INSERT INTO edge_fts VALUES(new.id,json_extract(new.json,'$.Title'),json_extract(new.json,'$.Content'),json_extract(new.json,'$.Tags'),json_extract(new.json,'$.OcrText')); END;
                CREATE TRIGGER IF NOT EXISTS edge_search_delete AFTER DELETE ON edge_items BEGIN DELETE FROM edge_fts WHERE id=old.id; END;
                CREATE TRIGGER IF NOT EXISTS edge_search_update AFTER UPDATE ON edge_items BEGIN
                  DELETE FROM edge_fts WHERE id=old.id;
                  INSERT INTO edge_fts VALUES(new.id,json_extract(new.json,'$.Title'),json_extract(new.json,'$.Content'),json_extract(new.json,'$.Tags'),json_extract(new.json,'$.OcrText')); END;
                INSERT INTO edge_fts SELECT id,json_extract(json,'$.Title'),json_extract(json,'$.Content'),json_extract(json,'$.Tags'),json_extract(json,'$.OcrText') FROM edge_items WHERE id NOT IN (SELECT id FROM edge_fts);
                INSERT OR IGNORE INTO edge_schema VALUES(2);
                """;
                await schema.ExecuteNonQueryAsync(); migration.Commit(); initialized=true;
            }
            return await work(db);
        }
        finally { gate.Release(); }
    }
    private static async Task<List<EdgeItem>> Read(SqliteConnection db, string? workspace = null, string? match = null)
    {
        using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT json FROM edge_items WHERE ($workspace IS NULL OR workspace=$workspace) AND ($match IS NULL OR id IN (SELECT id FROM edge_fts WHERE edge_fts MATCH $match))"; cmd.Parameters.AddWithValue("$match",(object?)match ?? DBNull.Value); cmd.Parameters.AddWithValue("$workspace", (object?)workspace ?? DBNull.Value);
        var result = new List<EdgeItem>(); await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(JsonSerializer.Deserialize<EdgeItem>(reader.GetString(0)) ?? throw new InvalidDataException("Unreadable Edge item; database was preserved."));
        return result;
    }
    public Task<IReadOnlyList<EdgeItem>> SearchAsync(string query, string? workspaceId = null, bool favourites = false, string? kind = null)
    {
        if(query.Length>4096) throw new ArgumentException("Search is limited to 4096 characters.");
        var terms=System.Text.RegularExpressions.Regex.Matches(query,@"[\p{L}\p{N}_]+").Select(x=>"\""+x.Value+"\"*").Take(16).ToArray();
        var match=terms.Length==0 ? null : string.Join(" AND ",terms);
        return With<IReadOnlyList<EdgeItem>>(async db => (await Read(db, workspaceId,match)).Where(x => (!favourites || x.Favourite) && (kind is null || x.Kind == kind) && (match is not null || (x.Title + "\n" + x.Content + "\n" + x.Tags + "\n" + x.OcrText).Contains(query, StringComparison.OrdinalIgnoreCase))).OrderByDescending(x => x.Pinned).ThenByDescending(x => x.Created).ToArray());
    }
    private static async Task Insert(SqliteConnection db, EdgeItem item, SqliteTransaction? transaction = null)
    {
        using var cmd = db.CreateCommand(); cmd.Transaction = transaction; cmd.CommandText = "INSERT OR IGNORE INTO edge_items(id,workspace,fingerprint,json) VALUES($id,$workspace,$fingerprint,$json)";
        cmd.Parameters.AddWithValue("$id",item.Id); cmd.Parameters.AddWithValue("$workspace",item.WorkspaceId); cmd.Parameters.AddWithValue("$fingerprint",item.Fingerprint); cmd.Parameters.AddWithValue("$json",JsonSerializer.Serialize(item)); await cmd.ExecuteNonQueryAsync();
    }
    public Task<EdgeItem> AddAsync(EdgeItem item)
    {
        EdgeRules.Validate(item);
        return With(async db => { await Insert(db,item); return (await Read(db,item.WorkspaceId)).First(x => x.Fingerprint == item.Fingerprint); });
    }
    public async Task UpdateAsync(EdgeItem item)
    {
        EdgeRules.Validate(item);
        await With(async db => { using var cmd=db.CreateCommand(); cmd.CommandText="UPDATE edge_items SET workspace=$workspace,fingerprint=$fingerprint,json=$json WHERE id=$id"; cmd.Parameters.AddWithValue("$workspace",item.WorkspaceId); cmd.Parameters.AddWithValue("$fingerprint",item.Fingerprint); cmd.Parameters.AddWithValue("$json",JsonSerializer.Serialize(item)); cmd.Parameters.AddWithValue("$id",item.Id); return await cmd.ExecuteNonQueryAsync(); });
    }
    public async Task RemoveAsync(IEnumerable<string> ids)
    {
        var selected=ids.Distinct().ToArray(); await With(async db => { using var transaction=db.BeginTransaction(); foreach(var id in selected) { using var cmd=db.CreateCommand(); cmd.Transaction=transaction; cmd.CommandText="DELETE FROM edge_items WHERE id=$id"; cmd.Parameters.AddWithValue("$id",id); await cmd.ExecuteNonQueryAsync(); } transaction.Commit(); return 0; });
    }
    public async Task<int> ClearExpiredAsync(DateTimeOffset now) => await With(async db => { var expired=(await Read(db)).Where(x => !x.Pinned && x.Expires <= now).ToArray(); using var transaction=db.BeginTransaction(); foreach(var item in expired) { using var cmd=db.CreateCommand(); cmd.Transaction=transaction; cmd.CommandText="DELETE FROM edge_items WHERE id=$id"; cmd.Parameters.AddWithValue("$id",item.Id); await cmd.ExecuteNonQueryAsync(); } transaction.Commit(); return expired.Length; });
    public Task<EdgeSettings> LoadSettingsAsync() => With(async db => { using var cmd=db.CreateCommand(); cmd.CommandText="SELECT json FROM edge_settings WHERE id=1"; var json=await cmd.ExecuteScalarAsync() as string; var settings=json is null ? new EdgeSettings() : JsonSerializer.Deserialize<EdgeSettings>(json) ?? throw new InvalidDataException("Unreadable Edge settings."); settings.Validate(); return settings; });
    public async Task SaveSettingsAsync(EdgeSettings settings)
    {
        settings.Validate(); await With(async db => { using var cmd=db.CreateCommand(); cmd.CommandText="INSERT INTO edge_settings VALUES(1,$json) ON CONFLICT(id) DO UPDATE SET json=excluded.json"; cmd.Parameters.AddWithValue("$json",JsonSerializer.Serialize(settings)); return await cmd.ExecuteNonQueryAsync(); });
    }
    public async Task ExportAsync(string output)
    {
        var items=await SearchAsync("");
        var images=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase); var managed=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!,"Edge","Images"));
        long assetBytes=0;
        foreach(var file in items.Select(x=>x.FilePath).Where(x=>x is not null).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var full=Path.GetFullPath(file!);
            if(Path.GetDirectoryName(full)==managed && File.Exists(full) && (File.GetAttributes(full)&FileAttributes.ReparsePoint)==0) { assetBytes+=new FileInfo(full).Length; if(assetBytes>22_000_000) throw new InvalidDataException("Managed images exceed the 32 MB JSON backup limit. Back up the profile folder for larger collections."); images[full]=Convert.ToBase64String(await File.ReadAllBytesAsync(full)); }
        }
        var bytes=JsonSerializer.SerializeToUtf8Bytes(new Backup(1,items.ToArray(),images)); if(bytes.Length>32_000_000) throw new InvalidDataException("Backup exceeds 32 MB. Export a profile copy for larger collections.");
        await using var stream=new FileStream(output,FileMode.CreateNew); await stream.WriteAsync(bytes);
    }
    public async Task RestoreAsync(string input)
    {
        if(new FileInfo(input).Length > 32_000_000) throw new InvalidDataException("Backup exceeds 32 MB.");
        var backup=JsonSerializer.Deserialize<Backup>(await File.ReadAllTextAsync(input)) ?? throw new InvalidDataException("Invalid Edge backup.");
        if(backup.Version != 1 || backup.Items is null || backup.Items.Length>10000) throw new InvalidDataException("Unsupported Edge backup.");
        foreach(var item in backup.Items) EdgeRules.Validate(item);
        var assets=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
        if(backup.Images is not null) foreach(var pair in backup.Images)
        {
            var bytes=Convert.FromBase64String(pair.Value);
            if(bytes.Length<8 || !bytes.AsSpan(0,8).SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 })) throw new InvalidDataException("Invalid managed screenshot in backup.");
            assets[pair.Key]=bytes;
        }
        var created=new List<string>();
        try
        {
            await With(async db =>
            {
                var existing=await Read(db); var folder=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!,"Edge","Images"); var replacements=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                using var transaction=db.BeginTransaction();
                foreach(var original in backup.Items)
                {
                    if(existing.Any(x=>x.Id==original.Id || (x.WorkspaceId==original.WorkspaceId && x.Fingerprint==original.Fingerprint))) continue;
                    var item=original;
                    if(item.FilePath is not null && assets.TryGetValue(item.FilePath,out var bytes))
                    {
                        if(!replacements.TryGetValue(item.FilePath,out var destination))
                        {
                            Directory.CreateDirectory(folder); destination=Path.Combine(folder,Guid.NewGuid().ToString("N")+".png");
                            await using var stream=new FileStream(destination,FileMode.CreateNew); created.Add(destination); await stream.WriteAsync(bytes); replacements[item.FilePath]=destination;
                        }
                        item=item with { FilePath=destination };
                    }
                    await Insert(db,item,transaction);
                }
                transaction.Commit(); return 0;
            });
        }
        catch { foreach(var file in created) if(File.Exists(file)) File.Delete(file); throw; }
    }
    private sealed record Backup(int Version, EdgeItem[] Items, Dictionary<string,string>? Images=null);
}
