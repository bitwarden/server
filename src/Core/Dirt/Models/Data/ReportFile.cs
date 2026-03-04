#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using static System.Text.Json.Serialization.JsonNumberHandling;

namespace Bit.Core.Dirt.Models.Data;

/// <summary>
/// Metadata about a file-backed organization report stored in blob storage.
/// Serialized into <see cref="Bit.Core.Dirt.Entities.OrganizationReport.ReportData"/>.
/// </summary>
public class ReportFile
{
    /// <summary>Validated byte-length of the blob (set after upload validation).</summary>
    [JsonNumberHandling(WriteAsString | AllowReadingFromString)]
    public long Size { get; set; }

    /// <summary>Random token that forms part of the blob path.</summary>
    [DisallowNull]
    public string? Id { get; set; }

    /// <summary>Leaf file name inside the blob path.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the blob has been validated after upload.
    /// Cloud uploads start <c>false</c> and are set to <c>true</c> by the Event Grid webhook.
    /// Self-hosted uploads are validated inline and default to <c>true</c>.
    /// </summary>
    public bool Validated { get; set; } = true;
}
