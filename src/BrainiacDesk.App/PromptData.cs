using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace BrainiacDesk.App;
public static class ProtectedLocalData
{
    public static void Save<T>(string name,T value)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(value);
        try { LocalConfiguration.Save(name,Convert.ToBase64String(ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser))); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public static T Load<T>(string name,T fallback)
    {
        var encoded=LocalConfiguration.Load<string?>(name,null); if(encoded is null) return fallback;
        var bytes=ProtectedData.Unprotect(Convert.FromBase64String(encoded),null,DataProtectionScope.CurrentUser);
        try { return JsonSerializer.Deserialize<T>(bytes) ?? throw new InvalidDataException("Saved data is invalid; it has not been reset."); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
public sealed record PromptPreferences(double Width=640,double Height=220,double X=0,double Y=0,bool HasPosition=false,string Monitor="",double FontSize=26,double Opacity=.96,double ReadingWidth=580,double Speed=30,bool ExcludeCapture=false,bool Mirror=false,string Alignment="Left",string TextColor="#F5F8FF",string BackgroundColor="#101B38",bool ReadingGuide=false,bool FlipVertical=false,double GuidePosition=.5);
public sealed record PromptScript(Guid Id,string Name,string Text,DateTimeOffset Updated);
public sealed record PromptLibrary(PromptScript[] Scripts,Guid? Active);
public sealed class ScriptLibrary
{
    public const int MaximumCharacters=250000;
    public List<PromptScript> Scripts { get; }
    public Guid? Active { get; private set; }
    public ScriptLibrary() { var saved=ProtectedLocalData.Load("scripts.protected.json",new PromptLibrary([],null)); Scripts=saved.Scripts.ToList(); Active=saved.Active; }
    public PromptScript Save(string name,string text)
    {
        name=name.Trim(); if(name.Length is <1 or >100 || text.Length>MaximumCharacters) throw new ArgumentException("Use a script name of 1–100 characters and text under 250,000 characters.");
        var script=new PromptScript(Active ?? Guid.NewGuid(),name,text,DateTimeOffset.UtcNow); var index=Scripts.FindIndex(x=>x.Id==script.Id);
        var next=Scripts.ToList(); if(index<0) next.Add(script); else next[index]=script;
        ProtectedLocalData.Save("scripts.protected.json",new PromptLibrary(next.ToArray(),script.Id)); Scripts.Clear(); Scripts.AddRange(next); Active=script.Id; return script;
    }
    public void Select(Guid? id) { if(id.HasValue && !Scripts.Any(x=>x.Id==id)) throw new ArgumentException("Script no longer exists."); Active=id; }
    public void Delete(Guid id) { var next=Scripts.Where(x=>x.Id!=id).ToArray(); var active=Active==id ? null : Active; ProtectedLocalData.Save("scripts.protected.json",new PromptLibrary(next,active)); Scripts.Clear(); Scripts.AddRange(next); Active=active; }
}
public static class PromptEngine
{
    public static double Advance(double offset,double elapsed,double speed,double maximum) => Math.Clamp(offset+Math.Clamp(elapsed,0,.1)*Math.Clamp(speed,5,180),0,Math.Max(0,maximum));
    // Require at least two consecutive recognized words, and only search a bounded forward window.
    public static int MatchSpeech(string script,string recognized,int cursor)
    {
        var words=Regex.Matches(script,@"[\p{L}\p{N}']+"); var heard=Regex.Matches(recognized,@"[\p{L}\p{N}']+").Select(x=>x.Value.ToLowerInvariant()).ToArray();
        if(heard.Length<2) return cursor; var start=0; while(start<words.Count && words[start].Index+words[start].Length<=cursor) start++;
        for(int i=Math.Max(0,start-3);i<Math.Min(words.Count,start+90);i++)
        {
            for(int h=0;h<heard.Length-1;h++)
            {
                int matched=0; while(i+matched<words.Count && h+matched<heard.Length && string.Equals(words[i+matched].Value,heard[h+matched],StringComparison.OrdinalIgnoreCase)) matched++;
                if(matched>=2) return Math.Max(cursor,words[i+matched-1].Index+words[i+matched-1].Length);
            }
        }
        return cursor;
    }
}