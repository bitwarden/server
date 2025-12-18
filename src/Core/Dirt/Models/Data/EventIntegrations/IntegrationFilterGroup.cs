namespace Bit.Core.Dirt.Models.Data.EventIntegrations;

public class IntegrationFilterGroup
{
    public bool AndOperator { get; init; } = true;
    public List<IntegrationFilterRule>? Rules { get; init; }
    public List<IntegrationFilterGroup>? Groups { get; init; }
}
