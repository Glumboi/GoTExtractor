using System.IO;
using ByteSizeLib;

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

    public GoTFile(string path)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path);
        NameWithSize = $"{Name} : {ByteSize.FromBytes(new FileInfo(path).Length).MegaBytes} MB";
    }
}