using System;
using System.IO;

public interface ILegacySaveFileProbe
{
    bool FileExists(string path);
}

public sealed class LegacySavePhysicalFileProbe : ILegacySaveFileProbe
{
    public bool FileExists(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.Directory) == 0;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }
}

public enum LegacySaveState
{
    Missing,
    Detected,
    Unavailable
}

public sealed class LegacySaveDetectionResult
{
    public LegacySaveState state;
    public bool isBlocking;
    public string message;
}

public sealed class LegacySaveDetector
{
    private readonly ILegacySaveFileProbe fileProbe;
    private readonly string legacySavePath;

    public LegacySaveDetector(ILegacySaveFileProbe fileProbe, string legacySavePath)
    {
        this.fileProbe = fileProbe ?? throw new ArgumentNullException(nameof(fileProbe));

        if (string.IsNullOrWhiteSpace(legacySavePath))
            throw new ArgumentException("A legacy save path is required.", nameof(legacySavePath));

        this.legacySavePath = legacySavePath;
    }

    public LegacySaveDetectionResult Detect()
    {
        try
        {
            bool detected = fileProbe.FileExists(legacySavePath);
            return new LegacySaveDetectionResult
            {
                state = detected ? LegacySaveState.Detected : LegacySaveState.Missing,
                isBlocking = false,
                message = detected
                    ? "检测到旧版存档。本版本不会自动转换；原文件已保留。"
                    : string.Empty
            };
        }
        catch (Exception exception)
        {
            return new LegacySaveDetectionResult
            {
                state = LegacySaveState.Unavailable,
                isBlocking = false,
                message = exception.Message
            };
        }
    }
}
