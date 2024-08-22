#nullable enable
using Bit.Core.Enums;

namespace Bit.Core.ActionableInsights.Models;

public class UrisReportParameters
{
    public string Uri { get; set; } = null!;

    public UriMatchType MatchType { get; set; }
}
