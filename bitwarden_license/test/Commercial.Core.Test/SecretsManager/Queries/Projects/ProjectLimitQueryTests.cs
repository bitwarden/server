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
public class ProjectLimitQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetByOrgIdAsync_OrganizationIsNull_ThrowsNotFound(SutProvider<ProjectLimitQuery> sutProvider,
        Guid organizationId)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(default).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.GetByOrgIdAsync(organizationId));

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
        SutProvider<ProjectLimitQuery> sutProvider, Organization organization)
    {
        organization.PlanType = planType;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.GetByOrgIdAsync(organization.Id));

        await sutProvider.GetDependency<IProjectRepository>().DidNotReceiveWithAnyArgs()
            .GetProjectCountByOrganizationIdAsync(organization.Id);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task GetByOrgIdAsync_SmNoneFreePlans_ReturnsNull(PlanType planType,
        SutProvider<ProjectLimitQuery> sutProvider, Organization organization)
    {
        organization.PlanType = planType;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var (limit, overLimit) = await sutProvider.Sut.GetByOrgIdAsync(organization.Id);

        Assert.Null(limit);
        Assert.Null(overLimit);

        await sutProvider.GetDependency<IProjectRepository>().DidNotReceiveWithAnyArgs()
            .GetProjectCountByOrganizationIdAsync(organization.Id);
    }

    [Theory]
    [BitAutoData(PlanType.Free, 0, false)]
    [BitAutoData(PlanType.Free, 1, false)]
    [BitAutoData(PlanType.Free, 2, false)]
    [BitAutoData(PlanType.Free, 3, true)]
    [BitAutoData(PlanType.Free, 4, true)]
    [BitAutoData(PlanType.Free, 40, true)]
    public async Task GetByOrgIdAsync_SmFreePlan_Success(PlanType planType, int projects, bool shouldBeOverLimit,
        SutProvider<ProjectLimitQuery> sutProvider, Organization organization)
    {
        organization.PlanType = planType;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IProjectRepository>().GetProjectCountByOrganizationIdAsync(organization.Id)
            .Returns(projects);

        var (limit, overLimit) = await sutProvider.Sut.GetByOrgIdAsync(organization.Id);

        Assert.NotNull(limit);
        Assert.NotNull(overLimit);
        Assert.Equal(3, limit.Value);
        Assert.Equal(shouldBeOverLimit, overLimit);

        await sutProvider.GetDependency<IProjectRepository>().Received(1)
            .GetProjectCountByOrganizationIdAsync(organization.Id);
    }
}
