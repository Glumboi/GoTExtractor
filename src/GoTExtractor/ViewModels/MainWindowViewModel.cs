﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ByteSizeLib;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using GoTExtractor.Core;
using MsBox.Avalonia;

namespace GoTExtractor.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private Process _currentUnpackerInstance;

    private const string _unPSARC = "ex\\UnPSARC.exe";

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
                    FileInfo.Clear();
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
                        Task.Run(UnpackPSARC);
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
            OnPropertyChanged(nameof(FileInfo));
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


    private bool _structurePreview = false;

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

    async void UnpackPSARC()
    {
        if (string.IsNullOrWhiteSpace(_selectedFile.Path))
        {
            await MessageBoxManager.GetMessageBoxStandard("Error",
                    $"You didn't select a psarc file in the TreeView!")
                .ShowAsync();
            return;
        }

        if (StructurePreview)
        {
            SubFiles.Clear();
            string tempDir = Path.GetTempFileName();
            tempDir = tempDir.Remove(tempDir.LastIndexOf('.'));
            string command = $"\"{SelectedFile.Path}\" \"{tempDir}\"";
            Directory.CreateDirectory(tempDir);
            DoPackerProcess(command, true);

            foreach (var f in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories))
            {
                SubFiles.Add(new GoTFile(f));
                File.Delete(f);
            }

            return;
        }

        var result = await GetFolderPickerResult("Select destination folder...");
        if (result != null)
        {
            string command = $"\"{SelectedFile}\" \"{result}\"";
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

    public MainWindowViewModel()
    {
        CreateCommands();
    }
}