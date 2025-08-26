
using Bit.Core.AdminConsole.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

public record SavePolicyModel(PolicyUpdate PolicyUpdate, IActingUser? PerformedBy, IPolicyMetadataModel Metadata)
{
    public PolicyUpdate PolicyUpdate { get; init; } = PolicyUpdate;
    public IPolicyMetadataModel Metadata { get; init; } = Metadata;

    public IActingUser? PerformedBy { get; init; } = PerformedBy;
}
