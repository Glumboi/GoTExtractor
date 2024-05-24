using System.IO;
using ByteSizeLib;

namespace GoTExtractor.Core;

public class GoTFile
{
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

        Name = System.IO.Path.GetFileName(path);
        NameWithSize = $"{Name} : {string.Format("{0:0.##}",ByteSize.FromBytes(new FileInfo(path).Length).MegaBytes)} MB";
    }

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
}