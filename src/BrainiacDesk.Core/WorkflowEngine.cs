namespace BrainiacDesk.Core;
public sealed record WorkflowStep(string Action);
public sealed record WorkflowDefinition(string Name, string InputFolder, string OutputFolder, string Extension, WorkflowStep[] Steps);
public static class WorkflowEngine
{
    public static string[] Preview(WorkflowDefinition workflow, string[] inputs)
    {
        Validate(workflow);
        foreach (var input in inputs) Authorize(input, workflow.InputFolder);
        var supported = inputs.Where(path => workflow.Extension.Length == 0 || Path.GetExtension(path).Equals(workflow.Extension, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (supported.Length == 0) throw new InvalidOperationException("No selected inputs match the workflow extension filter.");
        if (supported.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != supported.Length) throw new IOException("Input filenames collide.");
        return supported;
    }
    public static void Validate(WorkflowDefinition workflow)
    {
        if (string.IsNullOrWhiteSpace(workflow.Name) || workflow.Name.Length > 100 || workflow.Steps is null || workflow.Steps.Any(s => s is null) || workflow.Extension is null) throw new ArgumentException("Provide a workflow name, valid steps and extension filter.");
        if (!Directory.Exists(workflow.InputFolder) || !Directory.Exists(workflow.OutputFolder)) throw new DirectoryNotFoundException("Select existing input and output folders.");
        var input = Path.GetFullPath(workflow.InputFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var output = Path.GetFullPath(workflow.OutputFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (input.StartsWith(output, StringComparison.OrdinalIgnoreCase) || output.StartsWith(input, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Input/output folders must be separate, preventing workflow recursion.");
        Authorize(Path.Combine(output, ".brainiac-permission-probe"), workflow.OutputFolder);
        if (workflow.Steps.Length is 0 or > 20 || workflow.Steps.Any(s => s.Action is not ("Copy" or "SHA-256" or "ZIP"))) throw new InvalidOperationException("Workflow must contain 1–20 approved steps.");
        if (workflow.Steps.Count(x => x.Action == "Copy") > 1 || workflow.Steps.Count(x => x.Action == "SHA-256") > 1 || workflow.Steps.Count(x => x.Action == "ZIP") > 1) throw new InvalidOperationException("Repeated steps would collide. Use each action once.");
    }
    public static void Authorize(string path, string folder)
    {
        var root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("File is outside the approved folder.");
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(full)!); directory is not null; directory = directory.Parent) if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new UnauthorizedAccessException("Linked folders are not authorized.");
        if (File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new UnauthorizedAccessException("Linked files are not authorized.");
    }
    public static async Task RunAsync(WorkflowDefinition workflow, string[] selectedInputs, IProgress<string> progress, CancellationToken token)
    {
        var inputs = Preview(workflow, selectedInputs);
        var run = Path.Combine(workflow.OutputFolder, "Run-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(run);
        var log = new List<string>();
        try
        {
            foreach (var step in workflow.Steps)
            {
                token.ThrowIfCancellationRequested(); foreach (var input in inputs) Authorize(input, workflow.InputFolder); Authorize(Path.Combine(run, ".brainiac-permission-probe"), workflow.OutputFolder);
                progress.Report(step.Action);
                if (step.Action == "Copy") foreach (var input in inputs) { token.ThrowIfCancellationRequested(); await using var source = File.OpenRead(input); await using var destination = new FileStream(Path.Combine(run, Path.GetFileName(input)), FileMode.CreateNew); await source.CopyToAsync(destination, token); }
                else if (step.Action == "SHA-256") { var lines = new List<string>(); foreach (var input in inputs) lines.Add(await FileOperations.HashAsync(input, false, token) + "  " + Path.GetFileName(input)); await File.WriteAllLinesAsync(Path.Combine(run, "checksums.txt"), lines, token); }
                else if (step.Action == "ZIP") await FileOperations.CreateZipAsync(inputs, Path.Combine(run, "bundle.zip"), token);
                log.Add("Completed " + step.Action);
            }
            progress.Report("Completed: " + run);
        }
        catch (Exception ex) { log.Add("Stopped: " + ex.GetType().Name + ". Partial outputs retained for inspection."); throw; }
        finally { await File.WriteAllLinesAsync(Path.Combine(run, "workflow.log"), log, CancellationToken.None); }
    }
}