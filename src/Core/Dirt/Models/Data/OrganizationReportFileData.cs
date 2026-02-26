#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using static System.Text.Json.Serialization.JsonNumberHandling;

namespace Bit.Core.Dirt.Models.Data;

public class OrganizationReportFileData
{
    [JsonNumberHandling(WriteAsString | AllowReadingFromString)]
    public long Size { get; set; }

    [DisallowNull]
    public string? Id { get; set; }

    public string FileName { get; set; } = "report-data.json";

    public bool Validated { get; set; }
}
