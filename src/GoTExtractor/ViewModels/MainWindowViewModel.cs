using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ByteSizeLib;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using GoTExtractor.Core;
using GoTExtractor.LegacyPatternWindows;
using GoTExtractor.Views;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace GoTExtractor.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private Process _currentUnpackerInstance;

    private const string _unPSARC = "ex\\UnPSARC.exe";
    private const string _lastUnpacks = "lastunpacks.txt";
    private string _lastCratedTempDir = string.Empty;

    private string _subFileFilter = string.Empty;

    public string SubFileFilter
    {
        get => _subFileFilter;
        set
        {
            _subFileFilter = value;


            OnPropertyChanged(nameof(SubFileFilter));
        }
    }

    private GoTFile _selectedFile;

    public GoTFile SelectedFile
    {
        get => _selectedFile;
        set
        {
            _selectedFile = value;
            if (_selectedFile != null)
            {
                string f = value.Path;

                if (File.Exists(f))
                {
                    ClearLastTempFiles();

                    FileInfo.Clear();
                    SubFiles.Clear();
                    var fi = new FileInfo(f);
                    FileInfo.Add($"Name: {fi.Name}");
                    FileInfo.Add($"Directory name: {fi.DirectoryName}");
                    FileInfo.Add($"Size: {ByteSize.FromBytes(fi.Length).MegaBytes} MB");

                    Task.Run(() =>
                    {
                        FileInfo.Add($"MD5: Loading...");

                        using (var md5 = MD5.Create())
                        {
                            using (var stream = File.OpenRead(f))
                            {
                                var hash = md5.ComputeHash(stream);
                                FileInfo.Replace(FileInfo.Last(),
                                    $"MD5: {BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()}");
                            }
                        }
                    });


                    if (StructurePreview)
                    {
                        Task.Run(LoadStructurePreview);
                    }
                }

                OnPropertyChanged(nameof(SelectedFile));
            }
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

    private ObservableCollection<GoTFile> _subFiles = new();

    public ObservableCollection<GoTFile> SubFiles
    {
        get => _subFiles;
        set
        {
            _subFiles = value;
            OnPropertyChanged(nameof(SubFiles));
        }
    }

    private ObservableCollection<GoTFile> _files = new();

    public ObservableCollection<GoTFile> Files
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


    private bool _structurePreview = true;

    public bool StructurePreview
    {
        get => _structurePreview;
        set
        {
            _structurePreview = value;
            OnPropertyChanged(nameof(StructurePreview));
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
                if (Files.Count > 0)
                    Files.Clear();

                foreach (var file in Directory.GetFiles(result))
                {
                    if (Path.GetExtension(file) != ".psarc")
                        continue;

                    Files.Add(new GoTFile(file));
                }

                if (Files.Count < 1)
                {
                    await MessageBoxManager.GetMessageBoxStandard("Info", $"No psarc files found in: {result}!")
                        .ShowAsync();
                }
            }
        }
    }

    private ICommand RepackLastUnpacksCommand
    {
        get;
        set;
    }

    void RepackLastUnpacks()
    {
        foreach (var f in File.ReadLines(_lastUnpacks))
        {
            RepackPSARCEx(f);
        }
    }

    private ICommand ViewLastUnpackedCommand
    {
        get;
        set;
    }

    async void ViewLastUnpacked()
    {
        if (File.Exists(_lastUnpacks))
        {
            await MessageBoxManager.GetMessageBoxStandard("Last unpacked: ", File.ReadAllText(_lastUnpacks))
                .ShowAsync();
            return;
        }

        await MessageBoxManager.GetMessageBoxStandard("Info", "No unpacked files found!")
            .ShowAsync();
    }

    private ICommand UnpackAllGameFilesCommand
    {
        get;
        set;
    }

    async void UnpackAllGameFiles()
    {
        if (Files.Count <= 0)
        {
            MessageBoxManager.GetMessageBoxStandard("Info", "Please load the game psarc folder first!").ShowAsync();
            return;
        }

        string result = await GetFolderPickerResult("Select game extraction location");
        if (result != null)
        {
            foreach (var f in Files)
            {
                string outP = $"{result}\\{f.Name[..f.Name.LastIndexOf('.')]}";
                UnpackPSARCEx(outP, f.Path);
            }

            MessageBoxManager.GetMessageBoxStandard("Info", $"Extracted game files to: {result}").ShowAsync();
            return;
        }

        MessageBoxManager.GetMessageBoxStandard("Info", "Path is not found!").ShowAsync();
    }

    private ICommand DeleteLastUnpackedCommand
    {
        get;
        set;
    }

    async void DeleteLastUnpacked()
    {
        File.Delete(_lastUnpacks);
        await MessageBoxManager.GetMessageBoxStandard("Info", "Deleted last unpacked list!").ShowAsync();
    }

    private ICommand CloseDirectoryCommand
    {
        get;
        set;
    }

    void CloseDirectory()
    {
        SelectedFile = null;
        Files.Clear();
        FileInfo.Clear();
        SubFiles.Clear();
    }

    private ICommand UnpackPSARCCommand
    {
        get;
        set;
    }

    void LoadStructurePreview()
    {
        var load = new GoTFile("Loading...");

        SubFiles.Clear();
        SubFiles.Add(load);

        string tempDir = Path.GetTempFileName();
        tempDir = tempDir.Remove(tempDir.LastIndexOf('.'));
        _lastCratedTempDir = tempDir;
        string command = $"\"{SelectedFile.Path}\" \"{tempDir}\"";

        Directory.CreateDirectory(tempDir);

        DoPackerProcess(command, true);

        SubFiles.Remove(load);

        if (!Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);
        }

        foreach (var f in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories))
        {
            SubFiles.Add(new GoTFile(f));
        }
    }

    async void UnpackPSARC()
    {
        if (string.IsNullOrWhiteSpace(_selectedFile.Path))
        {
            await MessageBoxManager.GetMessageBoxStandard("Error",
                    $"You didn't select a psarc file in the TreeView!")
                .ShowAsync();
            return;
        }

        var result = await GetFolderPickerResult("Select destination folder...");
        if (result != null)
        {
            // Use temp files bu unpsarc
            if (StructurePreview && SubFiles.Count > 1)
            {
                foreach (var d in Directory.GetDirectories(_lastCratedTempDir, "*", SearchOption.AllDirectories))
                {
                    string newFolder = $"{result}\\{Path.GetFileName(d)}";
                    Directory.CreateDirectory(newFolder);
                    foreach (var f in Directory.GetFiles(d, "*", SearchOption.AllDirectories))
                    {
                        string newFile = $"{newFolder}\\{Path.GetFileName(f)}";
                        File.Copy(f, newFile);
                    }
                }

                foreach (var f in Directory.GetFiles(_lastCratedTempDir, "*", SearchOption.TopDirectoryOnly))
                {
                    string newFile = $"{result}\\{Path.GetFileName(f)}";
                    File.Copy(f, newFile);
                }

                ClearLastTempFiles();
            }
            else
            {
                UnpackPSARCEx(result, SelectedFile.Path);
            }

            await MessageBoxManager.GetMessageBoxStandard("Info",
                    $"If no erros occured, the extracted files can be found in {result}!")
                .ShowAsync();
        }
    }

    async void UnpackPSARCEx(string outPath, string targetFile)
    {
        if (outPath != null)
        {
            // Use temp files bu unpsarc
            string command = $"\"{targetFile}\" \"{outPath}\"";
            DoPackerProcess(command);

            await using (FileStream fs = new FileStream(_lastUnpacks, FileMode.Append))
            {
                byte[] file = new UTF8Encoding(true).GetBytes($"{outPath}\n");
                fs.Write(file, 0, file.Length);
            }
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
            RepackPSARCEx(result1);
        }
    }

    async void RepackPSARCEx(string inputFolder, bool askForSave = false)
    {
        if (!string.IsNullOrWhiteSpace(inputFolder))
        {
            string? res = null;
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                string? saveAs = string.Empty;
                if (askForSave)
                {
                    var dialog = new SaveFileDialog();
                    dialog.Title = "Save file as...";
                    dialog.InitialFileName = Path.GetFileName(inputFolder);

                    List<FileDialogFilter> filters = new List<FileDialogFilter>();
                    FileDialogFilter filter = new FileDialogFilter();
                    List<string> extension = new List<string>();
                    extension.Add("psarc");
                    filter.Extensions = extension;
                    filter.Name = "PSArchive Files";
                    filters.Add(filter);
                    dialog.Filters = filters;

                    saveAs = await dialog.ShowAsync(desktop.MainWindow);
                }
                else
                {
                    string repackDir =
                        $"{Path.GetFullPath("~Repackaged")}";
                    Directory.CreateDirectory(repackDir);
                    saveAs = $"{repackDir}\\{Path.GetFileName(inputFolder)}.psarc";
                }

                if (saveAs != null)
                {
                    string command = $"\"{inputFolder}\" \"{saveAs}\"";
                    DoPackerProcess(command);
                    if (!askForSave) return;
                    await MessageBoxManager.GetMessageBoxStandard("Info",
                            $"If no erros occured, the repacked file can be found as {saveAs}!")
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
        ViewLastUnpackedCommand = new RelayCommand(ViewLastUnpacked);
        DeleteLastUnpackedCommand = new RelayCommand(DeleteLastUnpacked);
        RepackLastUnpacksCommand = new RelayCommand(RepackLastUnpacks);
        UnpackAllGameFilesCommand = new RelayCommand(UnpackAllGameFiles);
    }

    async Task UnsupportedFeature()
    {
        await MessageBoxManager.GetMessageBoxStandard("Info", "Your platform doesn't support this feature!")
            .ShowAsync();
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

    void DoPackerProcess(string command, bool noWindow = false)
    {
        if (_currentUnpackerInstance != null)
        {
            if (!_currentUnpackerInstance.HasExited) _currentUnpackerInstance.Kill();
        }

        _currentUnpackerInstance = new Process();
        _currentUnpackerInstance.StartInfo.UseShellExecute = false;
        _currentUnpackerInstance.StartInfo.FileName = _unPSARC;
        _currentUnpackerInstance.StartInfo.Arguments = command;
        _currentUnpackerInstance.StartInfo.CreateNoWindow = noWindow;
        _currentUnpackerInstance.Start();
        _currentUnpackerInstance.WaitForExit();
    }

    void ClearLastTempFiles()
    {
        if (Directory.Exists(_lastCratedTempDir))
            Directory.Delete(_lastCratedTempDir, true);
    }

    public MainWindowViewModel()
    {
        CreateCommands();
    }
}