using Bit.Commercial.Core.SecretsManager.Queries.Projects;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Queries.Projects;

[SutProviderCustomize]
public class MaxProjectsQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetByOrgIdAsync_OrganizationIsNull_ThrowsNotFound(SutProvider<MaxProjectsQuery> sutProvider,
        Guid organizationId)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(default).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.GetByOrgIdAsync(organizationId, 1));

        await sutProvider.GetDependency<IProjectRepository>().DidNotReceiveWithAnyArgs()
            .GetProjectCountByOrganizationIdAsync(organizationId);
    }

    [Theory]
    [BitAutoData(PlanType.FamiliesAnnually2019)]
    [BitAutoData(PlanType.TeamsMonthly2019)]
    [BitAutoData(PlanType.TeamsAnnually2019)]
    [BitAutoData(PlanType.EnterpriseMonthly2019)]
    [BitAutoData(PlanType.EnterpriseAnnually2019)]
    [BitAutoData(PlanType.Custom)]
    [BitAutoData(PlanType.FamiliesAnnually)]
    public async Task GetByOrgIdAsync_SmPlanIsNull_ThrowsBadRequest(PlanType planType,
        SutProvider<MaxProjectsQuery> sutProvider, Organization organization)
    {
        organization.PlanType = planType;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.GetByOrgIdAsync(organization.Id, 1));

        await sutProvider.GetDependency<IProjectRepository>().DidNotReceiveWithAnyArgs()
            .GetProjectCountByOrganizationIdAsync(organization.Id);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task GetByOrgIdAsync_SmNoneFreePlans_ReturnsNull(PlanType planType,
        SutProvider<MaxProjectsQuery> sutProvider, Organization organization)
    {
        organization.PlanType = planType;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var (limit, overLimit) = await sutProvider.Sut.GetByOrgIdAsync(organization.Id, 1);

        Assert.Null(limit);
        Assert.Null(overLimit);

        await sutProvider.GetDependency<IProjectRepository>().DidNotReceiveWithAnyArgs()
            .GetProjectCountByOrganizationIdAsync(organization.Id);
    }

    [Theory]
    [BitAutoData(PlanType.Free, 0, 1, false)]
    [BitAutoData(PlanType.Free, 1, 1, false)]
    [BitAutoData(PlanType.Free, 2, 1, false)]
    [BitAutoData(PlanType.Free, 3, 1, true)]
    [BitAutoData(PlanType.Free, 4, 1, true)]
    [BitAutoData(PlanType.Free, 40, 1, true)]
    [BitAutoData(PlanType.Free, 0, 2, false)]
    [BitAutoData(PlanType.Free, 1, 2, false)]
    [BitAutoData(PlanType.Free, 2, 2, true)]
    [BitAutoData(PlanType.Free, 3, 2, true)]
    [BitAutoData(PlanType.Free, 4, 2, true)]
    [BitAutoData(PlanType.Free, 40, 2, true)]
    [BitAutoData(PlanType.Free, 0, 3, false)]
    [BitAutoData(PlanType.Free, 1, 3, true)]
    [BitAutoData(PlanType.Free, 2, 3, true)]
    [BitAutoData(PlanType.Free, 3, 3, true)]
    [BitAutoData(PlanType.Free, 4, 3, true)]
    [BitAutoData(PlanType.Free, 40, 3, true)]
    [BitAutoData(PlanType.Free, 0, 4, true)]
    [BitAutoData(PlanType.Free, 1, 4, true)]
    [BitAutoData(PlanType.Free, 2, 4, true)]
    [BitAutoData(PlanType.Free, 3, 4, true)]
    [BitAutoData(PlanType.Free, 4, 4, true)]
    [BitAutoData(PlanType.Free, 40, 4, true)]
    public async Task GetByOrgIdAsync_SmFreePlan__Success(PlanType planType, int projects, int projectsToAdd, bool expectedOverMax,
        SutProvider<MaxProjectsQuery> sutProvider, Organization organization)
    {
        organization.PlanType = planType;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IProjectRepository>().GetProjectCountByOrganizationIdAsync(organization.Id)
            .Returns(projects);

        var (max, overMax) = await sutProvider.Sut.GetByOrgIdAsync(organization.Id, projectsToAdd);

        Assert.NotNull(max);
        Assert.NotNull(overMax);
        Assert.Equal(3, max.Value);
        Assert.Equal(expectedOverMax, overMax);

        await sutProvider.GetDependency<IProjectRepository>().Received(1)
            .GetProjectCountByOrganizationIdAsync(organization.Id);
    }
}
