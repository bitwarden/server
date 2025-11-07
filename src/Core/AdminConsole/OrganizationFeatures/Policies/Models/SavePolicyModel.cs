
using Bit.Core.AdminConsole.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

public record SavePolicyModel(PolicyUpdate PolicyUpdate, IActingUser? PerformedBy, IPolicyMetadataModel Metadata)
{
    public SavePolicyModel(PolicyUpdate PolicyUpdate)
        : this(PolicyUpdate, null, new EmptyMetadataModel())
    {
    }

    public SavePolicyModel(PolicyUpdate PolicyUpdate, IActingUser performedBy)
        : this(PolicyUpdate, performedBy, new EmptyMetadataModel())
    {
    }

    public SavePolicyModel(PolicyUpdate PolicyUpdate, IPolicyMetadataModel metadata)
        : this(PolicyUpdate, null, metadata)
    {
    }
}
