using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using MsBox.Avalonia;

namespace GoTExtractor.LegacyPatternWindows;

public partial class ModMerger : Window
{
    private Process _currentUnpackerInstance;
    private const string _modsOut = "~ModsOut";
    private const string _mergedOut = "~MergedOut";
    private const string _modsFileList = _modsOut + "\\Filenames.txt";

    public ModMerger()
    {
        InitializeComponent();
    }

    private async void Add_Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Open the folder containing the mods you want to merge...";
            dialog.AllowMultiple = true;

            List<FileDialogFilter> filters = new List<FileDialogFilter>();
            FileDialogFilter filter = new FileDialogFilter();
            List<string> extension = new List<string>();
            extension.Add("psarc");
            filter.Extensions = extension;
            filter.Name = "PSArchive Files";
            filters.Add(filter);
            dialog.Filters = filters;

            var result = await dialog.ShowAsync(desktop.MainWindow);
            if (result != null)
            {
                foreach (var str in result)
                {
                    if (Path.GetExtension(str) == ".psarc")
                    {
                        Files_ListBox.Items.Add(str);
                        Program.appLog.Information($"Loading file {str} to merge list!");
                    }
                }
            }
        }
    }


    private void Remove_Button_OnClick(object? sender, RoutedEventArgs e)
    {
        Program.appLog.Information($"Removed {Files_ListBox.SelectedItem as string} from merge list!");
        Files_ListBox.Items.Remove(Files_ListBox.SelectedItem);
    }

    private void Clear_Button_OnClick(object? sender, RoutedEventArgs e)
    {
        Files_ListBox.Items.Clear();
        Program.appLog.Information("Cleared merge list!");
    }

    private async void AddFromFolder_Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var dialog = new OpenFolderDialog();
            dialog.Title = "Open the folder containing the mods you want to merge...";
            var result = await dialog.ShowAsync(desktop.MainWindow);
            if (result != null)
            {
                foreach (var file in Directory.GetFiles(result))
                {
                    if (Path.GetExtension(file) != ".psarc")
                        continue;

                    Files_ListBox.Items.Add(file);
                    Program.appLog.Information($"Loading file {file} to merge list!");
                }
            }
        }
    }

    private void Merge_Button_OnClick(object? sender, RoutedEventArgs e)
    {
        Extract();
        Repack();
    }

    void Repack()
    {
        string outPsarc = $"{Path.GetFullPath(_mergedOut)}\\{MergedName_TextBox.Text}.psarc";
        string command =
            $"\"{Path.GetFullPath(_modsOut)}\" \"{outPsarc}\"";
        if (!Directory.Exists(_mergedOut)) Directory.CreateDirectory(_mergedOut);
        RunProcess(command);
        Directory.Delete(_modsOut, true);
        string message = $"Merged mods!\nOutput in: {outPsarc}";
        MessageBoxManager.GetMessageBoxStandard("Info", message).ShowAsync();
        Program.appLog.Information(message);
    }

    void Extract()
    {
        if (!Directory.Exists(_modsOut))
        {
            Directory.CreateDirectory(_modsOut);
        }

        string lastFileNames = string.Empty;
        foreach (string f in Files_ListBox.Items)
        {
            string command = $"\"{f}\" \"{Path.GetFullPath(_modsOut)}\"";
            if (File.Exists(_modsFileList))
                lastFileNames = File.ReadAllText(_modsFileList);

            RunProcess(command);
            if (!string.IsNullOrWhiteSpace(lastFileNames))
            {
                var stream = File.AppendText(_modsFileList);
                stream.Write(lastFileNames);
                stream.Close();
            }
        }

        //Cleanup of filenames.txt (remove all first '\' if there are any)

        List<string> cleanedContent = new();
        foreach (var line in File.ReadLines(_modsFileList))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line[0] != '/') continue;
            string cleaned = line.Substring(1, line.Length - 1);
            cleanedContent.Add(cleaned);
        }

        File.WriteAllLines(_modsFileList, cleanedContent.ToArray());
        Program.appLog.Information(
            $"Filenames.txt of {MergedName_TextBox.Text}: \n{string.Join("\n", cleanedContent)}");
    }

    void RunProcess(string command, bool noWindow = false)
    {
        if (_currentUnpackerInstance != null)
        {
            if (!_currentUnpackerInstance.HasExited) _currentUnpackerInstance.Kill();
        }

        _currentUnpackerInstance = new Process();
        _currentUnpackerInstance.StartInfo.UseShellExecute = false;
        _currentUnpackerInstance.StartInfo.FileName = "ex\\UnPSARC.exe";
        _currentUnpackerInstance.StartInfo.Arguments = command;
        _currentUnpackerInstance.StartInfo.CreateNoWindow = noWindow;
        Program.appLog.Information($"Launching UnPSARC with args:\n{command}");
        _currentUnpackerInstance.Start();
        _currentUnpackerInstance.WaitForExit();
    }
}