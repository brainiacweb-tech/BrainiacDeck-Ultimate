using System;
using System.IO;
using System.Linq;
namespace BrainiacDesk.App;
public sealed record OperationRecord(DateTimeOffset Time,string Module,string Input,string Output,string Outcome,long Bytes);
public static class OperationJournal
{
    public static event Action? Changed;
    public static string? LastError { get; private set; }
    public static OperationRecord[] Load()=>ProtectedLocalData.Load("operations.protected.json",Array.Empty<OperationRecord>());
    public static void Record(string module,string input,string output,string outcome)
    {
        try { var bytes=File.Exists(output) ? new FileInfo(output).Length : 0; var records=Load().Prepend(new OperationRecord(DateTimeOffset.UtcNow,module,input,output,outcome,bytes)).Take(200).ToArray(); ProtectedLocalData.Save("operations.protected.json",records); LastError=null; Changed?.Invoke(); }
        catch(Exception ex) { LastError="Operation completed, but history could not be saved: "+ex.Message; }
    }
    public static void Clear() { ProtectedLocalData.Save("operations.protected.json",Array.Empty<OperationRecord>()); Changed?.Invoke(); }
    public static string Summary()
    {
        try { var scripts=new ScriptLibrary().Scripts.Count; var spaces=LocalConfiguration.Load("workspaces.json",new Workspace[0]).Length; var operations=Load(); return $"{scripts} saved scripts · {spaces} saved workspaces · {operations.Count(x=>x.Module=="Converter" && x.Outcome=="Completed")} completed conversions in retained history"; }
        catch(Exception ex) { return "Local data unavailable: "+ex.Message; }
    }
}