using System.Text;
using System.Text.Json;

namespace Bit.DataBoundaries.Output;

internal static class ReportWriter
{
    private static readonly JsonSerializerOptions _writeOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly UTF8Encoding _utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Serializes with LF endings on every platform so regenerated output diffs cleanly in CI.
    /// </summary>
    public static string Serialize(OwnershipReport report) =>
        JsonSerializer.Serialize(report, _writeOptions).ReplaceLineEndings("\n") + "\n";

    public static void Write(string path, string content)
    {
        if (Path.GetDirectoryName(path) is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content, _utf8NoBom);
    }
}
