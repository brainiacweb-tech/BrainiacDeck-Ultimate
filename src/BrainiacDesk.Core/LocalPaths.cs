namespace BrainiacDesk.Core;
public static class LocalPaths
{
    public static string Data => Environment.GetEnvironmentVariable("BRAINIACDESK_DATA") is { Length: > 0 } path ? Path.GetFullPath(path) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BrainiacDesk");
}
