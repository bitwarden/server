#nullable enable
using Bit.Core.Enums;

namespace Bit.Core.ActionableInsights.Entities;

public class ReportParameters
{
    public IEnumerable<ApplicationReportParameters>? Applications { get; set; }
}

public class ApplicationReportParameters
{
    public string Name { get; set; } = null!;

    public required IEnumerable<UrisReportParameters> Uris { get; set; }
}

public class UrisReportParameters
{
    public string Uri { get; set; } = null!;

    public UriMatchType MatchType { get; set; }
}
