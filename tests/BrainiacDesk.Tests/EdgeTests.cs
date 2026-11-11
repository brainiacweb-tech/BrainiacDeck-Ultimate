using BrainiacDesk.Core;
using BrainiacDesk.Infrastructure;
internal static class EdgeTests
{
    public static async Task Run(string path, Action<bool,string> check)
    {
        var store=new EdgeStore(path);
        check(await store.LoadSettingsAsync() == new EdgeSettings(),"Edge additive schema defaults");
        var item=new EdgeItem(Guid.NewGuid().ToString(),"Text","Research","quoted ' content",null,"workspace-a",EdgeRules.Fingerprint("Text","quoted ' content"),DateTimeOffset.UtcNow, OcrText:"recognized words");
        var added=await store.AddAsync(item); var duplicate=await store.AddAsync(item with { Id=Guid.NewGuid().ToString() });
        check(added.Id==duplicate.Id,"Edge duplicate insertion returns original");
        await Task.WhenAll(Enumerable.Range(0,12).Select(_=>store.AddAsync(item with { Id=Guid.NewGuid().ToString() })));
        check((await store.SearchAsync("", "workspace-a")).Count==1,"Edge concurrent deduplication");
        await store.AddAsync(item with { Id=Guid.NewGuid().ToString(),WorkspaceId="workspace-b" });
        check((await store.SearchAsync("recognized","workspace-a")).Count==1,"Edge OCR text search and workspace isolation");
        check((await store.SearchAsync("' content")).Count==2,"Edge literal search handles quotes");
        await store.UpdateAsync(item with { Favourite=true, Tags="coding" });
        check((await store.SearchAsync("coding", "workspace-a",true)).Count==1,"Edge metadata and favourites persist");
        var expired=item with { Id=Guid.NewGuid().ToString(),Fingerprint=EdgeRules.Fingerprint("Text","expired"),Pinned=false,Expires=DateTimeOffset.UtcNow.AddDays(-1) };
        await store.AddAsync(expired); check(await store.ClearExpiredAsync(DateTimeOffset.UtcNow)==1,"Edge expiry removes only unpinned items");
        await store.SaveSettingsAsync(new EdgeSettings { Width=700 }); check((await new EdgeStore(path).LoadSettingsAsync()).Width==700,"Edge settings survive restart");
        check((await new SettingsStore(path).LoadAsync()).OnboardingComplete,"Edge migration preserves existing settings");
        var backup=Path.Combine(Path.GetDirectoryName(path)!,"edge-backup.json"); await store.ExportAsync(backup);
        await store.RemoveAsync([item.Id]); await store.RestoreAsync(backup); await store.RestoreAsync(backup);
        check((await new EdgeStore(path).SearchAsync("")).Count==2,"Edge merge restore is idempotent and survives restart");
        var bad=Path.Combine(Path.GetDirectoryName(path)!,"bad-edge.json"); await File.WriteAllTextAsync(bad,"{\"Version\":2,\"Items\":[]}");
        bool rejected=false; try { await store.RestoreAsync(bad); } catch(InvalidDataException) { rejected=true; }
        check(rejected && (await store.SearchAsync("")).Count==2,"Edge rejects unsupported backup without losing content");
        await store.UpdateAsync(item with { Title="Changed title",Content="new searchable payload",Tags="updatedtag",OcrText="new OCR",Favourite=true });
        check((await store.SearchAsync("updatedtag")).Count==1 && (await store.SearchAsync("recognized")).Count==1,"Edge FTS update replaces stale indexed fields");
        var indexed=(await store.SearchAsync("updatedtag")).Single(); await store.RemoveAsync([indexed.Id]); check((await store.SearchAsync("updatedtag")).Count==0,"Edge FTS deletion removes indexed content");
        var migrationPath=Path.Combine(Path.GetDirectoryName(path)!,"legacy-edge.db");
        await using(var db=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+migrationPath))
        {
            await db.OpenAsync(); using var command=db.CreateCommand(); command.CommandText="CREATE TABLE edge_items(id TEXT PRIMARY KEY,workspace TEXT NOT NULL,fingerprint TEXT NOT NULL,json TEXT NOT NULL,UNIQUE(workspace,fingerprint)); CREATE TABLE edge_schema(version INTEGER PRIMARY KEY); INSERT INTO edge_schema VALUES(1); INSERT INTO edge_items VALUES($id,$workspace,$fingerprint,$json)";
            command.Parameters.AddWithValue("$id",item.Id); command.Parameters.AddWithValue("$workspace",item.WorkspaceId); command.Parameters.AddWithValue("$fingerprint",item.Fingerprint); command.Parameters.AddWithValue("$json",System.Text.Json.JsonSerializer.Serialize(item)); await command.ExecuteNonQueryAsync();
        }
        check((await new EdgeStore(migrationPath).SearchAsync("recognized")).Count==1,"Edge version-one migration backfills full-text index without losing pins");
        await using(var db=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+migrationPath)) { await db.OpenAsync(); using var command=db.CreateCommand(); command.CommandText="INSERT INTO edge_schema VALUES(99)"; await command.ExecuteNonQueryAsync(); }
        bool future=false; try { await new EdgeStore(migrationPath).LoadSettingsAsync(); } catch(InvalidDataException) { future=true; } check(future,"Edge refuses newer database schema without resetting data");
        var dwell=new EdgeDwell(); check(!dwell.Tick(true,0,300) && !dwell.Tick(false,400,300) && !dwell.Tick(true,500,300) && dwell.Tick(true,800,300),"Edge dwell resets on departure");
        check(EdgeRules.AtEdge(-100,-500,-1920,-500,0,580,"Top",2) && !EdgeRules.AtEdge(-100,-497,-1920,-500,0,580,"Top",2),"Edge physical negative-monitor coordinates and sensitivity");
    }
}
