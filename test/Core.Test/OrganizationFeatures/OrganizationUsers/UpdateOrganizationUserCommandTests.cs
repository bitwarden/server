using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class UpdateOrganizationUserCommandTests
{
    [Theory, BitAutoData]
    public async Task UpdateUserAsync_NoUserId_Throws(OrganizationUser user, Guid? savingUserId,
        IEnumerable<CollectionAccessSelection> collections, IEnumerable<Guid> groups, SutProvider<UpdateOrganizationUserCommand> sutProvider)
    {
        user.Id = default(Guid);
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateUserAsync(user, savingUserId, collections, groups));
        Assert.Contains("invite the user first", exception.Message.ToLowerInvariant());
    }

    [Theory, BitAutoData]
    public async Task UpdateUserAsync_NoChangeToData_Throws(OrganizationUser user, Guid? savingUserId,
        IEnumerable<CollectionAccessSelection> collections, IEnumerable<Guid> groups, SutProvider<UpdateOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        organizationUserRepository.GetByIdAsync(user.Id).Returns(user);
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateUserAsync(user, savingUserId, collections, groups));
        Assert.Contains("make changes before saving", exception.Message.ToLowerInvariant());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateUserAsync_WithoutConfirmedOwners_Throws(
        OrganizationUser oldUserData,
        OrganizationUser newUserData,
        IEnumerable<CollectionAccessSelection> collections, IEnumerable<Guid> groups, SutProvider<UpdateOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        organizationUserRepository.GetByIdAsync(oldUserData.Id).Returns(oldUserData);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateUserAsync(newUserData, null, collections, groups));
        Assert.Contains("Organization must have at least one confirmed owner.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateUserAsync_Passes(
        Organization organization,
        OrganizationUser oldUserData,
        OrganizationUser newUserData,
        IEnumerable<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups,
        Permissions permissions,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser savingUser,
        SutProvider<UpdateOrganizationUserCommand> sutProvider)
    {
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        newUserData.Id = oldUserData.Id;
        newUserData.UserId = oldUserData.UserId;
        newUserData.OrganizationId = savingUser.OrganizationId = oldUserData.OrganizationId = organization.Id;
        newUserData.AccessAll = false;
        newUserData.Permissions = CoreHelpers.ClassToJsonData(permissions);

        oldUserData.AccessSecretsManager = false;

        organizationUserRepository.GetByIdAsync(oldUserData.Id).Returns(oldUserData);
        organizationUserRepository.GetManyByOrganizationAsync(savingUser.OrganizationId, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser> { savingUser });
        organizationService.HasConfirmedOwnersExceptAsync(
            organization.Id, Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(newUserData.Id))).Returns(true);

        await sutProvider.Sut.UpdateUserAsync(newUserData, savingUser.UserId, collections, groups);

        await organizationService.Received(1)
            .ValidateOrganizationUserUpdatePermissions(
                newUserData.OrganizationId, newUserData.Type, oldUserData.Type,
                Arg.Is<Permissions>(p => p.ClaimsMap.All(c => permissions.ClaimsMap.Any(pcm => pcm.Permission == c.Permission && pcm.ClaimName == c.ClaimName))));
        await organizationUserRepository.Received(1)
            .ReplaceAsync(newUserData, collections);
        await organizationUserRepository.Received(1)
            .UpdateGroupsAsync(newUserData.Id, groups);
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationUserEventAsync(newUserData, EventType.OrganizationUser_Updated);
    }
}
