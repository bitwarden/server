#nullable enable

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public class IntegrationFilterRule
{
    public required string Property { get; set; }
    public required IntegrationFilterOperation Operation { get; set; }
    public required object? Value { get; set; }
}

