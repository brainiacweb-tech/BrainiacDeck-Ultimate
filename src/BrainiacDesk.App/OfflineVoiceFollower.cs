using System;
using System.Linq;
using System.Speech.Recognition;
namespace BrainiacDesk.App;
public sealed class OfflineVoiceFollower : IDisposable
{
    private SpeechRecognitionEngine? engine;
    public static string[] Languages() => SpeechRecognitionEngine.InstalledRecognizers().Select(x=>x.Culture.Name).Distinct().ToArray();
    public void Start(string language,Action<string,float> recognized,Action<string> error)
    {
        Dispose();
        try
        {
            var installed=SpeechRecognitionEngine.InstalledRecognizers().FirstOrDefault(x=>x.Culture.Name==language) ?? throw new InvalidOperationException("No offline speech recognizer is installed for this language. Install a Windows speech language first.");
            engine=new SpeechRecognitionEngine(installed); engine.LoadGrammar(new DictationGrammar());
            engine.SpeechRecognized+=(_,e)=> { if(e.Result.Confidence>=.55f) recognized(e.Result.Text,e.Result.Confidence); };
            engine.RecognizeCompleted+=(_,e)=> { if(e.Error is not null) error(e.Error.Message); };
            engine.SetInputToDefaultAudioDevice(); engine.RecognizeAsync(RecognizeMode.Multiple);
        }
        catch { Dispose(); throw; }
    }
    public void Dispose() { var current=engine; engine=null; if(current is null) return; try { current.RecognizeAsyncCancel(); } finally { current.Dispose(); } }
}