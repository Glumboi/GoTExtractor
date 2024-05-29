using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using ByteSizeLib;
using CommunityToolkit.Mvvm.Input;
using GoTExtractor.ViewModels;
using MsBox.Avalonia;

namespace GoTExtractor.Core;

public class GoTFile
{
    public string Path
    {
        get;
    }

    public string Name
    {
        get;
    }

    public string NameWithSize
    {
        get;
    }

    public string ParentPath
    {
        get => Directory.GetParent(Directory.GetParent(Path).FullName).FullName;
    }

    private ICommand OpenSubFileInExplorerCommand
    {
        get;
        set;
    }

    void OpenSubFileInExplorer()
    {
        if (!File.Exists(Path))
        {
            MessageBoxManager.GetMessageBoxStandard("Error", "File does not exist!").ShowAsync();
        }

        if (OperatingSystem.IsWindows())
        {
            Process.Start("explorer.exe", "/select, " + Path);
        }
    }

    private ICommand RemoveFileCommand
    {
        get;
        set;
    }

    void RemoveFile()
    {
        if (!File.Exists(Path))
            return;

        string filenamesTxt = ParentPath + "\\Filenames.txt";
        Program.appLog.Information($"Deleting {Path}");
        File.Delete(Path);
        List<string> newFilenamesTxt = new();

        foreach (var line in File.ReadLines(filenamesTxt))
        {
            if (!line.ToLower().Contains(Path.Split('\\').Last().ToLower()))
            {
                newFilenamesTxt.Add(line);
                continue;
            }

            Program.appLog.Information($"Removed {line} from Filenames.txt of {Name}");
        }

        File.WriteAllLines(filenamesTxt, newFilenamesTxt.ToArray());
        Program.appLog.Information($"New Filenames.txt:\n{string.Join("\n", newFilenamesTxt)}");
        MessageBoxManager.GetMessageBoxStandard("Placeholder info",
            $"Removed file {Name} from archive!\nTHIS IS A PLACEHOLDER MESSAGEBOX UNTIL A BETTER UX IS FOUND!").ShowAsync();
    }

    public GoTFile(string path)
    {
        Path = path;

        // Not an actual file now but I don't want to create 2 seperate structs just for displaying info
        if (!File.Exists(path))
        {
            NameWithSize = path;
            Name = path;
            return;
        }

        OpenSubFileInExplorerCommand = new RelayCommand(OpenSubFileInExplorer, () =>
        {
            return File.Exists(path);
        });
        RemoveFileCommand = new RelayCommand(RemoveFile, () =>
        {
            return File.Exists(path) && !path.Contains("Filenames.txt");
        });

        Name = System.IO.Path.GetFileName(path);
        NameWithSize =
            $"{Name} : {string.Format("{0:0.##}", ByteSize.FromBytes(new FileInfo(path).Length).MegaBytes)} MB";
    }
}