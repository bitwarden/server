

using Bit.Core.AdminConsole.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

public record SavePolicyModel(PolicyUpdate Data, IActingUser? PerformedBy, IPolicyMetadataModel Metadata)
{
    public PolicyUpdate Data { get; init; } = Data;
    public IPolicyMetadataModel Metadata { get; init; } = Metadata;

    public IActingUser? PerformedBy { get; init; } = PerformedBy;
}
