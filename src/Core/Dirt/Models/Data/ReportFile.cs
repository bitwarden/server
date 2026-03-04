using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using static System.Text.Json.Serialization.JsonNumberHandling;

namespace Bit.Core.Dirt.Models.Data;

public class ReportFile
{
    /// <summary>
    /// Uniquely identifies an uploaded file.
    /// </summary>
    [DisallowNull]
    public string? Id { get; set; }

    /// <summary>
    /// Attached file name.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Size of the attached file in bytes.
    /// </summary>
    [JsonNumberHandling(WriteAsString | AllowReadingFromString)]
    public long Size { get; set; }

    /// <summary>
    /// When true the uploaded file's length has been validated.
    /// </summary>
    public bool Validated { get; set; } = true;
}
