#nullable enable
namespace Bit.Core.ActionableInsights.Models;

public class ApplicationReportParameters
{
    public string Name { get; set; } = null!;

    public required IEnumerable<UrisReportParameters> Uris { get; set; }
}
