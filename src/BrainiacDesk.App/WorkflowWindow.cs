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
    public WorkflowWindow(Func<string[],Task>? pinToEdge = null, EdgeCoordinator? edge = null) : base("Workflow Automation Studio")
    {
        Note("Build a manual file workflow. Each step operates on the selected, filtered source files and writes into a new run folder. No scripts or shell commands are executed. Folder watchers and schedules are not included.");
        var pinCompleted=new CheckBox { Content="Pin reviewed source file references to the active Edge workspace after success",IsChecked=false }; if(pinToEdge is not null) Body.Children.Add(pinCompleted);
        Note("Workflow name"); var name = Input(); Note("Approved input folder"); var input = Input(); Note("Approved output folder"); var output = Input(); Note("Extension filter (optional)"); var extension = Input();
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
        Accessible(name,"Workflow name"); Accessible(input,"Approved input folder"); Accessible(output,"Approved output folder"); Accessible(extension,"Extension filter"); Accessible(action,"Workflow step action"); Accessible(list,"Workflow steps");
        var previewText = new TextBox { IsReadOnly = true, Height = 130, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Body.Children.Add(previewText); Accessible(previewText,"Workflow dry-run results");
        Action("Select files / dry run", () => { files = PickFiles(); reviewed = Definition(); preview = WorkflowEngine.Preview(reviewed, files); previewText.Text = string.Join(" → ", reviewed.Steps.Select(x => x.Action)) + Environment.NewLine + string.Join(Environment.NewLine, preview.Select(path => path + " → " + reviewed.OutputFolder)); Status.Text = "Dry run only. No files changed."; return Task.CompletedTask; });
        Action("Run reviewed workflow", async () => { if (cancellation is not null) return; var definition = Definition(); if (preview is null || reviewed is null || JsonSerializer.Serialize(definition) != JsonSerializer.Serialize(reviewed)) throw new InvalidOperationException("Dry-run the current workflow first."); if (MessageBox.Show(DialogOwner, $"Run {definition.Steps.Length} steps for {preview.Length} files in the approved output folder?", "Confirm workflow", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return; cancellation = new(); try { var progress = new Progress<string>(text => Status.Text = text); await Task.Run(() => WorkflowEngine.RunAsync(definition, preview, progress, cancellation.Token)); if(pinCompleted.IsChecked==true && pinToEdge is not null) await pinToEdge(preview); ActivityLog.Record("Workflow completed"); OperationJournal.Record("Workflow", definition.Name, definition.OutputFolder, "Completed"); } catch (OperationCanceledException) { Status.Text = "Workflow cancelled. Partial outputs and log retained."; } finally { cancellation.Dispose(); cancellation = null; preview = null; } });
        Action("Cancel execution", () => { cancellation?.Cancel(); return Task.CompletedTask; });
        Action("Save workflow", () => { var definition = Definition(); WorkflowEngine.Validate(definition); LocalConfiguration.Save("workflow.json", definition); Status.Text = "Workflow saved locally."; return Task.CompletedTask; });
        Action("Load saved workflow", () => { var definition = LocalConfiguration.Load<WorkflowDefinition?>("workflow.json", null) ?? throw new InvalidOperationException("No saved workflow."); name.Text = definition.Name; input.Text = definition.InputFolder; output.Text = definition.OutputFolder; extension.Text = definition.Extension; steps.Clear(); foreach (var step in definition.Steps) steps.Add(step); preview = null; return Task.CompletedTask; });
        if(edge is not null)
        {
            Note("Edge image workflow: pin image, optional offline OCR, Clipboard Shelf and optional PDF. Source images remain unchanged. Partial results remain if a later step fails.");
            var recognize=new CheckBox { Content="Recognize text locally",IsChecked=true }; var toShelf=new CheckBox { Content="Send recognized text through Clipboard Shelf privacy filters",IsChecked=false }; var pdf=new CheckBox { Content="Export image as a new PDF using Converter",IsChecked=false }; Body.Children.Add(recognize); Body.Children.Add(toShelf); Body.Children.Add(pdf);
            Action("Run Edge image workflow",async ()=> { var images=PickFiles("Images|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff"); if(images.Length==0) return; string? destination=null; if(pdf.IsChecked==true) { destination=SaveFile("PDF|*.pdf",".pdf"); if(destination is null) return; if(images.Length!=1) throw new InvalidOperationException("Choose one image for PDF workflow export, or export multiple pins from Edge."); } foreach(var image in images) await edge.RunImageWorkflowAsync(image,recognize.IsChecked==true,toShelf.IsChecked==true,destination); Status.Text="Edge image workflow completed."; });
        }
        Closing += (_, e) => { if (cancellation is not null) { cancellation.Cancel(); e.Cancel = true; Status.Text = "Stopping workflow; close after it stops."; } };
    }
}