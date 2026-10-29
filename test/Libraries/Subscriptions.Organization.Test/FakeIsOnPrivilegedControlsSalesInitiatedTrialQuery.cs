using Bit.Subscriptions.Organization.Queries;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Test;

internal sealed class FakeIsOnPrivilegedControlsSalesInitiatedTrialQuery : IIsOnPrivilegedControlsSalesInitiatedTrialQuery
{
    public bool Result { get; set; }
    public Exception? Exception { get; set; }
    public OrganizationEntity? ReceivedOrganization { get; private set; }

    public Task<bool> Run(OrganizationEntity organization)
    {
        ReceivedOrganization = organization;
        return Exception is null ? Task.FromResult(Result) : Task.FromException<bool>(Exception);
    }
}
