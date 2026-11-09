using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace BrainiacDesk.App;
public class ToolWindow : Window
{
    private int activeOperations;
    public bool IsBusy => activeOperations > 0;
    protected readonly StackPanel Body = new() { Margin = new Thickness(24) };
    protected readonly TextBlock Status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 6) };
    public ToolWindow(string title)
    {
        Title = title + " · BrainiacDesk"; Width = 880; Height = 700; MinWidth = 650; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = new ScrollViewer { Content = Body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Body.Children.Add(new TextBlock { Text = title, FontSize = 28, Margin = new Thickness(0, 0, 0, 16) });
        Body.Children.Add(Status);
    }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (IsBusy) { e.Cancel = true; Status.Text = "Wait for the active operation to finish, or use its Cancel control, before closing."; }
        base.OnClosing(e);
    }
    protected void Note(string text) => Body.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) });
    protected Button Action(string text, Func<Task> action, Panel? parent = null)
    {
        var button = new Button { Content = text };
        button.Click += async (_, _) => { button.IsEnabled = false; activeOperations++; try { await action(); } catch (Exception ex) { Status.Text = ex.Message; } finally { activeOperations--; button.IsEnabled = true; } };
        (parent ?? Body).Children.Add(button); return button;
    }
    protected TextBox Input(string text = "", int height = 32)
    {
        var input = new TextBox { Text = text, Height = height, Margin = new Thickness(0, 6, 0, 6), Padding = new Thickness(8), AcceptsReturn = height > 50, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Body.Children.Add(input); return input;
    }
    protected static string[] PickFiles(string filter = "All files|*.*") { var dialog = new OpenFileDialog { Multiselect = true, Filter = filter }; return dialog.ShowDialog() == true ? dialog.FileNames : []; }
    protected static string? PickFolder() { var dialog = new OpenFolderDialog(); return dialog.ShowDialog() == true ? dialog.FolderName : null; }
    protected static string? SaveFile(string filter, string extension) { var dialog = new SaveFileDialog { Filter = filter, DefaultExt = extension, AddExtension = true }; return dialog.ShowDialog() == true ? dialog.FileName : null; }
    protected static string AppData => BrainiacDesk.Core.LocalPaths.Data;
}