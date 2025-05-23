﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class GetOrganizationUsersClaimedStatusQueryTests
{
    [Theory, BitAutoData]
    public async Task GetUsersOrganizationManagementStatusAsync_WithNoUsers_ReturnsEmpty(
        Organization organization,
        SutProvider<GetOrganizationUsersClaimedStatusQuery> sutProvider)
    {
        var result = await sutProvider.Sut.GetUsersOrganizationClaimedStatusAsync(organization.Id, new List<Guid>());

        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task GetUsersOrganizationManagementStatusAsync_WithUseOrganizationDomainsEnabled_Success(
        Organization organization,
        ICollection<OrganizationUser> usersWithClaimedDomain,
        SutProvider<GetOrganizationUsersClaimedStatusQuery> sutProvider)
    {
        organization.Enabled = true;
        organization.UseOrganizationDomains = true;

        var userIdWithoutClaimedDomain = Guid.NewGuid();
        var userIdsToCheck = usersWithClaimedDomain.Select(u => u.Id).Concat(new List<Guid> { userIdWithoutClaimedDomain }).ToList();

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility(organization));

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationWithClaimedDomainsAsync(organization.Id)
            .Returns(usersWithClaimedDomain);

        var result = await sutProvider.Sut.GetUsersOrganizationClaimedStatusAsync(organization.Id, userIdsToCheck);

        Assert.All(usersWithClaimedDomain, ou => Assert.True(result[ou.Id]));
        Assert.False(result[userIdWithoutClaimedDomain]);
    }

    [Theory, BitAutoData]
    public async Task GetUsersOrganizationManagementStatusAsync_WithUseOrganizationDomainsDisabled_ReturnsAllFalse(
        Organization organization,
        ICollection<OrganizationUser> usersWithClaimedDomain,
        SutProvider<GetOrganizationUsersClaimedStatusQuery> sutProvider)
    {
        organization.Enabled = true;
        organization.UseOrganizationDomains = false;

        var userIdWithoutClaimedDomain = Guid.NewGuid();
        var userIdsToCheck = usersWithClaimedDomain.Select(u => u.Id).Concat(new List<Guid> { userIdWithoutClaimedDomain }).ToList();

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility(organization));

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationWithClaimedDomainsAsync(organization.Id)
            .Returns(usersWithClaimedDomain);

        var result = await sutProvider.Sut.GetUsersOrganizationClaimedStatusAsync(organization.Id, userIdsToCheck);

        Assert.All(result, r => Assert.False(r.Value));
    }

    [Theory, BitAutoData]
    public async Task GetUsersOrganizationManagementStatusAsync_WithDisabledOrganization_ReturnsAllFalse(
        Organization organization,
        ICollection<OrganizationUser> usersWithClaimedDomain,
        SutProvider<GetOrganizationUsersClaimedStatusQuery> sutProvider)
    {
        organization.Enabled = false;

        var userIdWithoutClaimedDomain = Guid.NewGuid();
        var userIdsToCheck = usersWithClaimedDomain.Select(u => u.Id).Concat(new List<Guid> { userIdWithoutClaimedDomain }).ToList();

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility(organization));

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationWithClaimedDomainsAsync(organization.Id)
            .Returns(usersWithClaimedDomain);

        var result = await sutProvider.Sut.GetUsersOrganizationClaimedStatusAsync(organization.Id, userIdsToCheck);

        Assert.All(result, r => Assert.False(r.Value));
    }
}
