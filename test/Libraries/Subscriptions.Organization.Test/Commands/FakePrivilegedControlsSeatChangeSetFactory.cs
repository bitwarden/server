using Bit.Core.Billing.Commands;
using Bit.Subscriptions.Organization.Commands;
using Bit.Subscriptions.Organization.Models;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Test.Commands;

// The factory interface is internal, so NSubstitute can't proxy it without InternalsVisibleTo for Castle.
internal sealed class FakePrivilegedControlsSeatChangeSetFactory : IPrivilegedControlsSeatChangeSetFactory
{
    public BillingCommandResult<PrivilegedControlsSeatChange>? Result { get; set; }
    public List<(OrganizationEntity Organization, int Seats)> Calls { get; } = [];

    public Task<BillingCommandResult<PrivilegedControlsSeatChange>> CreateAsync(
        OrganizationEntity organization, int seats)
    {
        Calls.Add((organization, seats));
        return Task.FromResult(Result ?? throw new InvalidOperationException("No result was configured."));
    }
}
