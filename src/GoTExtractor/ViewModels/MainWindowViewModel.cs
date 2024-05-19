using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ByteSizeLib;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;

namespace GoTExtractor.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private const string unPSARC = "ex\\UnPSARC.exe";

    private string _fileLog = String.Empty;

    public string FileLog
    {
        get => _fileLog;
        set
        {
            _fileLog = value;
            OnPropertyChanged(nameof(FileLog));
        }
    }

    private string _selectedFile = null;

    public string SelectedFile
    {
        get => _selectedFile;
        set
        {
            _selectedFile = value;

            string f = $"{CurrentDirectory}\\{value}";

            if (File.Exists(f))
            {
                FileInfo.Clear();
                var fi = new FileInfo(f);
                FileInfo.Add($"Name: {fi.Name}");
                FileInfo.Add($"Directory name: {fi.DirectoryName}");
                /*using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(f))
                    {
                        var hash = md5.ComputeHash(stream);
                        FileInfo.Add($"MD5: {BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()}");
                    }
                }*/

                FileInfo.Add($"Size: {ByteSize.FromBytes(fi.Length).MegaBytes} MB");
            }

            OnPropertyChanged(nameof(SelectedFile));
        }
    }

    private ObservableCollection<string> _fileInfo = new();

    public ObservableCollection<string> FileInfo
    {
        get => _fileInfo;
        set
        {
            _fileInfo = value;
            OnPropertyChanged(nameof(FileInfo));
        }
    }


    private ObservableCollection<string> _files = new();

    public ObservableCollection<string> Files
    {
        get => _files;
        set
        {
            _files = value;
            OnPropertyChanged(nameof(Files));
        }
    }

    private string _currentDirectory = string.Empty;

    public string CurrentDirectory
    {
        get => _currentDirectory;
        set
        {
            _currentDirectory = value;
            OnPropertyChanged(nameof(CurrentDirectory));
        }
    }


    private ICommand OpenDirectoryCommand
    {
        get;
        set;
    }

    async void OpenDirectory()
    {
        if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var dialog = new OpenFolderDialog();
            dialog.Title = "Open the GoT psarc folder...";
            var result = await dialog.ShowAsync(desktop.MainWindow);
            if (result != null)
            {
                CurrentDirectory = result;
                Files.Clear();
                foreach (var file in Directory.GetFiles(result))
                {
                    if (Path.GetExtension(file) != ".psarc")
                        continue;

                    Files.Add(Path.GetFileName(file));
                }

                if (Files.Count < 1)
                {
                    await MessageBoxManager.GetMessageBoxStandard("Info", $"No psarc files found in: {result}!")
                        .ShowAsync();
                }
            }
        }
    }

    private ICommand CloseDirectoryCommand
    {
        get;
        set;
    }

    void CloseDirectory()
    {
        SelectedFile = String.Empty;
        Files.Clear();
        FileInfo.Clear();
    }

    private ICommand UnpackPSARCCommand
    {
        get;
        set;
    }

    async void UnpackPSARC()
    {
        if (string.IsNullOrWhiteSpace(_selectedFile))
        {
            await MessageBoxManager.GetMessageBoxStandard("Error",
                    $"You didn't select a psarc file in the TreeView!")
                .ShowAsync();
            return;
        }

        var result = await GetFolderPickerResult("Select destination folder...");
        if (result != null)
        {
            string command = $"\"{CurrentDirectory}\\{SelectedFile}\" \"{result}\"";
            DoPackerProcess(command);
            await MessageBoxManager.GetMessageBoxStandard("Info",
                    $"If no erros occured, the extracted files can be found in {result}!")
                .ShowAsync();
        }
    }

    private ICommand RepackPSARCCommand
    {
        get;
        set;
    }

    async void RepackPSARC()
    {
        var result1 = await GetFolderPickerResult("Select folder to repack...");
        if (result1 != null)
        {
            string? res = null;
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var dialog = new SaveFileDialog();
                dialog.Title = "Save file as...";

                List<FileDialogFilter> filters = new List<FileDialogFilter>();
                FileDialogFilter filter = new FileDialogFilter();
                List<string> extension = new List<string>();
                extension.Add("psarc");
                filter.Extensions = extension;
                filter.Name = "PSArchive Files";
                filters.Add(filter);
                dialog.Filters = filters;

                string result2 = await dialog.ShowAsync(desktop.MainWindow);
                if (result2 != null)
                {
                    string command = $"\"{result1}\" \"{result2}\"";
                    DoPackerProcess(command);
                    await MessageBoxManager.GetMessageBoxStandard("Info",
                            $"If no erros occured, the repacked file can be found as {result2}!")
                        .ShowAsync();
                }
            }
        }
    }

    void CreateCommands()
    {
        OpenDirectoryCommand = new RelayCommand(OpenDirectory);
        CloseDirectoryCommand = new RelayCommand(CloseDirectory);
        UnpackPSARCCommand = new RelayCommand(UnpackPSARC);
        RepackPSARCCommand = new RelayCommand(RepackPSARC);
    }

    async Task<string?> GetFolderPickerResult(string pickerTitle)
    {
        string? res = null;
        if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var dialog = new OpenFolderDialog();
            dialog.Title = pickerTitle;
            res = await dialog.ShowAsync(desktop.MainWindow);
        }

        return res;
    }

    void DoPackerProcess(string command)
    {
        Process p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = unPSARC;
        p.StartInfo.Arguments = command;
        p.Start();
        p.WaitForExit();
    }

    public MainWindowViewModel()
    {
        CreateCommands();
    }
}