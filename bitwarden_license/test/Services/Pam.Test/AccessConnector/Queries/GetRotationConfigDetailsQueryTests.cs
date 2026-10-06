using Bit.Core.Exceptions;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Queries;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
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
            .GetManyByConfigIdAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ConfigBelongsToAnotherOrganization_ThrowsNotFound(
        SutProvider<GetRotationConfigDetailsQuery> sutProvider, Guid organizationId,
        PamRotationConfigDetails details)
    {
        details.OrganizationId = Guid.NewGuid();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(organizationId, details.Id));

        // The history read is expensive and would leak; it must not happen at all.
        await sutProvider.GetDependency<IPamRotationJobRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByConfigIdAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ConfigInTheRouteOrganization_ReturnsDetailsWithJobHistory(
        PamRotationConfigDetails details, List<PamRotationJobDetails> jobs)
    {
        var now = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        var sutProvider = new SutProvider<GetRotationConfigDetailsQuery>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(now);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        // Statuses are derived against the query's own clock.
        sutProvider.GetDependency<IPamRotationJobRepository>().GetManyByConfigIdAsync(details.Id, now).Returns(jobs);

        var result = await sutProvider.Sut.GetAsync(details.OrganizationId, details.Id);

        Assert.Same(details, result.Config);
        Assert.Equal(jobs.Count, result.Jobs.Count);
    }
}
