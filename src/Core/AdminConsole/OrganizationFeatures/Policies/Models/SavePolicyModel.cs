
using Bit.Core.AdminConsole.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

public record SavePolicyModel(PolicyUpdate PolicyUpdate, IActingUser? PerformedBy, IPolicyMetadataModel Metadata)
{
}
