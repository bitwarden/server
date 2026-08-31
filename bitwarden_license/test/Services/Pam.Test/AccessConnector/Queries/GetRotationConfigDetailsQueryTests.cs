using Bit.Core.Exceptions;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Queries;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.AccessConnector.Queries;

/// <summary>
/// ManageAccessConnectorRequirement only proves the caller administers the organization named in the route, so this query's
/// own OrganizationId check is the sole thing keeping an Owner of one organization from reading another's rotation
/// config and its full job/attempt history.
/// </summary>
[SutProviderCustomize]
public class GetRotationConfigDetailsQueryTests
{
    [Theory, BitAutoData]
    public async Task GetAsync_ConfigMissing_ThrowsNotFound(
        SutProvider<GetRotationConfigDetailsQuery> sutProvider, Guid organizationId, Guid configId)
    {
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(configId)
            .Returns((PamRotationConfigDetails?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(organizationId, configId));

        await sutProvider.GetDependency<IPamRotationJobRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByConfigIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ConfigBelongsToAnotherOrganization_ThrowsNotFound(
        SutProvider<GetRotationConfigDetailsQuery> sutProvider, Guid organizationId,
        PamRotationConfigDetails details)
    {
        details.OrganizationId = Guid.NewGuid();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(organizationId, details.Id));

        // The history read is the expensive part and the part that would leak; it must not happen at all.
        await sutProvider.GetDependency<IPamRotationJobRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByConfigIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ConfigInTheRouteOrganization_ReturnsDetailsWithJobHistory(
        SutProvider<GetRotationConfigDetailsQuery> sutProvider, PamRotationConfigDetails details,
        List<PamRotationJobDetails> jobs)
    {
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        sutProvider.GetDependency<IPamRotationJobRepository>().GetManyByConfigIdAsync(details.Id).Returns(jobs);

        var result = await sutProvider.Sut.GetAsync(details.OrganizationId, details.Id);

        Assert.Same(details, result.Config);
        Assert.Equal(jobs.Count, result.Jobs.Count);
    }
}
