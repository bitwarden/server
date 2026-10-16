using Bit.Core.AdminConsole.Entities;

namespace Bit.Api.AdminConsole.Partnerships;

public sealed class PartnershipRequestContext : IPartnershipRequestContext
{
    private OrganizationPartnership? _partnership;

    public OrganizationPartnership Partnership =>
        _partnership ?? throw new InvalidOperationException("The partnership has not been resolved for this request.");

    public void SetPartnership(OrganizationPartnership partnership) => _partnership = partnership;
}
