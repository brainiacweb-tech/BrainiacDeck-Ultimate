using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BrainiacDesk.Core;
namespace BrainiacDesk.App;
public sealed record DashboardItem(string Id,string Title,string Category,string Detail,object Payload);
public sealed record DashboardActivity(string Title,string Detail,string Time);
public sealed record ToolTile(string Id,string Title,string Description,string Color,string Icon,string AutomationName);
public static class DashboardData
{
    public static readonly ToolTile[] Tools=[
        new("prompt","Teleprompter","Present with confidence. Your words, at your pace.","LavenderCard","Prompt","Open teleprompter"),
        new("capture","Screen Capture","Record, capture and annotate your best ideas.","MintCard","Capture","Open capture"),
        new("convert","File Converter","Beautifully simple image and PDF exports.","PeachCard","Convert","Open converter"),
        new("edge","Brainiac Edge","Keep notes, code and inspiration within reach.","BlueCard","Shelf","Open Brainiac Edge"),
        new("shelf","Clipboard Shelf","Save useful snippets, images and file references.","PinkCard","Shelf","Open shelf"),
        new("files","File Toolkit","Organize, verify, rename and archive your files.","CyanCard","Files","Open file toolkit"),
        new("workflow","Workflow Studio","Bring repeatable file tasks into one routine.","LavenderCard","Workflow","Open workflows"),
        new("workspaces","Workspaces","Bring the right tools together for each project.","MintCard","Workspace","Open workspaces")];
    public static DashboardItem[] Workspaces()=>WorkspaceWindow.LoadWorkspaces().OrderByDescending(x=>x.Updated).Select(x=>new DashboardItem(x.Id,x.Name,"Project workspace",$"{x.Actions.Length} tools · "+(x.Updated is { } time ? time.ToLocalTime().ToString("dd MMM yyyy") : "Saved collection"),x)).ToArray();
    public static DashboardItem[] Scripts()=>new ScriptLibrary().Scripts.OrderByDescending(x=>x.Updated).Select(x=>new DashboardItem(x.Id.ToString(),x.Name,"Teleprompter script",x.Updated.ToLocalTime().ToString("dd MMM yyyy · HH:mm"),x)).ToArray();
    public static DashboardItem[] Outputs()=>OperationJournal.Load().Where(x=>x.Outcome=="Completed" && !string.IsNullOrWhiteSpace(x.Output)).GroupBy(x=>x.Output,StringComparer.OrdinalIgnoreCase).Select(x=>x.First()).Select(x=>new DashboardItem(x.Output,Path.GetFileName(x.Output),x.Module,File.Exists(x.Output) || Directory.Exists(x.Output) ? $"{x.Time.ToLocalTime():dd MMM · HH:mm} · {x.Bytes:N0} bytes" : "Original output is missing",x)).ToArray();
    public static DashboardActivity[] Activity()=>LocalConfiguration.Load("activity.json",Array.Empty<string>()).Take(12).Select(x=>new DashboardActivity(x.Length>21 ? x[21..] : x,"Application activity",x.Length>=20 ? x[..20] : "")).ToArray();
    public static async Task<DashboardItem[]> SearchAsync(string query,IEdgeStore edge)
    {
        query=query.Trim(); if(query.Length==0) return [];
        var result=new List<DashboardItem>();
        result.AddRange(ActionRouter.Catalog.Where(x=>ActionRouter.Score(x,query)>0).OrderByDescending(x=>ActionRouter.Score(x,query)).Take(8).Select(x=>new DashboardItem(x.Id,x.Title,"Tool or command",x.Keywords,x)));
        result.AddRange(Workspaces().Concat(Scripts()).Concat(Outputs()).Where(x=>(x.Title+" "+x.Category).Contains(query,StringComparison.OrdinalIgnoreCase)).Take(12));
        result.AddRange((await edge.SearchAsync(query)).Take(8).Select(x=>new DashboardItem(x.Id,x.Title,"Edge pin",x.Kind,x)));
        return result.ToArray();
    }
}
