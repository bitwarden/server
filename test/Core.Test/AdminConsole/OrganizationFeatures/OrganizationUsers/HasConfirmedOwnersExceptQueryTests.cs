using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class HasConfirmedOwnersExceptQueryTests
{
    [Theory, BitAutoData]
    public async Task HasConfirmedOwnersExcept_WithConfirmedOwner_WithNoException_ReturnsTrue(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        SutProvider<HasConfirmedOwnersExceptQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser> { owner });

        var result = await sutProvider.Sut.HasConfirmedOwnersExceptAsync(organization.Id, new List<Guid>(), true);

        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task HasConfirmedOwnersExcept_ExcludingConfirmedOwner_ReturnsFalse(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        SutProvider<HasConfirmedOwnersExceptQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser> { owner });

        var result = await sutProvider.Sut.HasConfirmedOwnersExceptAsync(organization.Id, new List<Guid> { owner.Id }, true);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task HasConfirmedOwnersExcept_WithInvitedOwner_ReturnsFalse(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Invited, OrganizationUserType.Owner)] OrganizationUser owner,
        SutProvider<HasConfirmedOwnersExceptQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser> { owner });

        var result = await sutProvider.Sut.HasConfirmedOwnersExceptAsync(organization.Id, new List<Guid>(), true);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task HasConfirmedOwnersExcept_WithConfirmedProviderUser_IncludeProviderTrue_ReturnsTrue(
        bool includeProvider,
        Organization organization,
        ProviderUser providerUser,
        SutProvider<HasConfirmedOwnersExceptQuery> sutProvider)
    {
        providerUser.Status = ProviderUserStatusType.Confirmed;

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByOrganizationAsync(organization.Id, ProviderUserStatusType.Confirmed)
            .Returns(new List<ProviderUser> { providerUser });

        var result = await sutProvider.Sut.HasConfirmedOwnersExceptAsync(organization.Id, new List<Guid>(), includeProvider);

        Assert.Equal(includeProvider, result);
    }

    [Theory, BitAutoData]
    public async Task HasConfirmedOwnersExceptAsync_WithConfirmedOwners_ReturnsTrue(
        Guid organizationId,
        IEnumerable<Guid> organizationUsersId,
        ICollection<OrganizationUser> owners,
        SutProvider<HasConfirmedOwnersExceptQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationAsync(organizationId, OrganizationUserType.Owner)
            .Returns(owners);

        var result = await sutProvider.Sut.HasConfirmedOwnersExceptAsync(organizationId, organizationUsersId);

        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task HasConfirmedOwnersExceptAsync_WithConfirmedProviders_ReturnsTrue(
        Guid organizationId,
        IEnumerable<Guid> organizationUsersId,
        ICollection<ProviderUser> providerUsers,
        SutProvider<HasConfirmedOwnersExceptQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationAsync(organizationId, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser>());

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByOrganizationAsync(organizationId, ProviderUserStatusType.Confirmed)
            .Returns(providerUsers);

        var result = await sutProvider.Sut.HasConfirmedOwnersExceptAsync(organizationId, organizationUsersId);

        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task HasConfirmedOwnersExceptAsync_WithNoConfirmedOwnersOrProviders_ReturnsFalse(
        Guid organizationId,
        IEnumerable<Guid> organizationUsersId,
        SutProvider<HasConfirmedOwnersExceptQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationAsync(organizationId, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser>());

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByOrganizationAsync(organizationId, ProviderUserStatusType.Confirmed)
            .Returns(new List<ProviderUser>());

        var result = await sutProvider.Sut.HasConfirmedOwnersExceptAsync(organizationId, organizationUsersId);

        Assert.False(result);
    }
}
