using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BrainiacDesk.Core;
namespace BrainiacDesk.App;
public sealed class WorkflowWindow : ToolWindow
{
    private CancellationTokenSource? cancellation;
    public WorkflowWindow() : base("Workflow Automation Studio")
    {
        Note("Build a manual file workflow. Each step operates on the selected, filtered source files and writes into a new run folder. No scripts or shell commands are executed. Folder watchers and schedules are not included.");
        var name = Input("My workflow"); var input = Input(); var output = Input(); var extension = Input(".png");
        Action("Approve input folder", () => { var path = PickFolder(); if (path is not null) input.Text = path; return Task.CompletedTask; });
        Action("Approve output folder", () => { var path = PickFolder(); if (path is not null) output.Text = path; return Task.CompletedTask; });
        Note("Extension filter above; empty matches all selected files.");
        var steps = new ObservableCollection<WorkflowStep>(); var list = new ListBox { ItemsSource = steps, DisplayMemberPath = "Action", Height = 130 }; Body.Children.Add(list);
        var action = new ComboBox { ItemsSource = new[] { "Copy", "SHA-256", "ZIP" }, SelectedIndex = 0 }; Body.Children.Add(action);
        Action("Add step ↓", () => { steps.Add(new((string)action.SelectedItem)); return Task.CompletedTask; });
        Action("Remove selected step", () => { if (list.SelectedItem is WorkflowStep step) steps.Remove(step); return Task.CompletedTask; });
        Action("Move step up", () => { if (list.SelectedItem is WorkflowStep step && steps.IndexOf(step) > 0) steps.Move(steps.IndexOf(step), steps.IndexOf(step) - 1); return Task.CompletedTask; });
        string[] files = []; string[]? preview = null; WorkflowDefinition? reviewed = null;
        WorkflowDefinition Definition() => new(name.Text, input.Text, output.Text, extension.Text.Trim(), steps.ToArray());
        var previewText = new TextBox { IsReadOnly = true, Height = 130, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Body.Children.Add(previewText);
        Action("Select files / dry run", () => { files = PickFiles(); reviewed = Definition(); preview = WorkflowEngine.Preview(reviewed, files); previewText.Text = string.Join(" → ", reviewed.Steps.Select(x => x.Action)) + Environment.NewLine + string.Join(Environment.NewLine, preview.Select(path => path + " → " + reviewed.OutputFolder)); Status.Text = "Dry run only. No files changed."; return Task.CompletedTask; });
        Action("Run reviewed workflow", async () => { if (cancellation is not null) return; var definition = Definition(); if (preview is null || reviewed is null || JsonSerializer.Serialize(definition) != JsonSerializer.Serialize(reviewed)) throw new InvalidOperationException("Dry-run the current workflow first."); if (MessageBox.Show(this, $"Run {definition.Steps.Length} steps for {preview.Length} files in the approved output folder?", "Confirm workflow", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return; cancellation = new(); try { var progress = new Progress<string>(text => Status.Text = text); await Task.Run(() => WorkflowEngine.RunAsync(definition, preview, progress, cancellation.Token)); ActivityLog.Record("Workflow completed"); } catch (OperationCanceledException) { Status.Text = "Workflow paused by cancellation. Partial outputs and log retained."; } finally { cancellation.Dispose(); cancellation = null; preview = null; } });
        Action("Pause / cancel execution", () => { cancellation?.Cancel(); return Task.CompletedTask; });
        Action("Save workflow", () => { var definition = Definition(); WorkflowEngine.Validate(definition); LocalConfiguration.Save("workflow.json", definition); Status.Text = "Workflow saved locally."; return Task.CompletedTask; });
        Action("Load saved workflow", () => { var definition = LocalConfiguration.Load<WorkflowDefinition?>("workflow.json", null) ?? throw new InvalidOperationException("No saved workflow."); name.Text = definition.Name; input.Text = definition.InputFolder; output.Text = definition.OutputFolder; extension.Text = definition.Extension; steps.Clear(); foreach (var step in definition.Steps) steps.Add(step); preview = null; return Task.CompletedTask; });
        Closing += (_, e) => { if (cancellation is not null) { cancellation.Cancel(); e.Cancel = true; Status.Text = "Stopping workflow; close after it stops."; } };
    }
}