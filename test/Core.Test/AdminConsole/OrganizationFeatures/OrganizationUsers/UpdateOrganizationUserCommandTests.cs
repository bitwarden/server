using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class UpdateOrganizationUserCommandTests
{
    [Theory, BitAutoData]
    public async Task UpdateUserAsync_NoUserId_Throws(OrganizationUser user, Guid? savingUserId,
        ICollection<CollectionAccessSelection> collections, IEnumerable<Guid> groups, SutProvider<UpdateOrganizationUserCommand> sutProvider)
    {
        user.Id = default(Guid);
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateUserAsync(user, savingUserId, collections, groups));
        Assert.Contains("invite the user first", exception.Message.ToLowerInvariant());
    }

    [Theory, BitAutoData]
    public async Task UpdateUserAsync_NoChangeToData_Throws(OrganizationUser user, Guid? savingUserId,
        ICollection<CollectionAccessSelection> collections, IEnumerable<Guid> groups, SutProvider<UpdateOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        organizationUserRepository.GetByIdAsync(user.Id).Returns(user);
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateUserAsync(user, savingUserId, collections, groups));
        Assert.Contains("make changes before saving", exception.Message.ToLowerInvariant());
    }

    [Theory, BitAutoData]
    public async Task UpdateUserAsync_Passes(
        Organization organization,
        OrganizationUser oldUserData,
        OrganizationUser newUserData,
        ICollection<CollectionAccessSelection> collections,
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
        newUserData.Permissions = JsonSerializer.Serialize(permissions, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        organizationUserRepository.GetByIdAsync(oldUserData.Id).Returns(oldUserData);
        organizationUserRepository.GetManyByOrganizationAsync(savingUser.OrganizationId, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser> { savingUser });
        organizationService
            .HasConfirmedOwnersExceptAsync(
                newUserData.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(i => i.Contains(newUserData.Id)))
            .Returns(true);

        await sutProvider.Sut.UpdateUserAsync(newUserData, savingUser.UserId, collections, groups);

        await organizationService.Received(1).ValidateOrganizationUserUpdatePermissions(
            newUserData.OrganizationId,
            newUserData.Type,
            oldUserData.Type,
            Arg.Any<Permissions>());
        await organizationService.Received(1).ValidateOrganizationCustomPermissionsEnabledAsync(
            newUserData.OrganizationId,
            newUserData.Type);
        await organizationService.Received(1).HasConfirmedOwnersExceptAsync(
            newUserData.OrganizationId,
            Arg.Is<IEnumerable<Guid>>(i => i.Contains(newUserData.Id)));
    }

    [Theory, BitAutoData]
    public async Task UpdateUserAsync_WithFlexibleCollections_WhenUpgradingToManager_Throws(
        Organization organization,
        [OrganizationUser(type: OrganizationUserType.User)] OrganizationUser oldUserData,
        [OrganizationUser(type: OrganizationUserType.Manager)] OrganizationUser newUserData,
        [OrganizationUser(type: OrganizationUserType.Owner, status: OrganizationUserStatusType.Confirmed)] OrganizationUser savingUser,
        ICollection<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups,
        SutProvider<UpdateOrganizationUserCommand> sutProvider)
    {
        organization.FlexibleCollections = true;
        newUserData.Id = oldUserData.Id;
        newUserData.UserId = oldUserData.UserId;
        newUserData.OrganizationId = oldUserData.OrganizationId = savingUser.OrganizationId = organization.Id;
        newUserData.Permissions = CoreHelpers.ClassToJsonData(new Permissions());

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IOrganizationService>()
            .HasConfirmedOwnersExceptAsync(newUserData.OrganizationId, Arg.Is<IEnumerable<Guid>>(i => i.Contains(newUserData.Id)))
            .Returns(true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(oldUserData.Id)
            .Returns(oldUserData);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser> { savingUser });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateUserAsync(newUserData, oldUserData.UserId, collections, groups));

        Assert.Contains("manager role has been deprecated", exception.Message.ToLowerInvariant());
    }

    [Theory, BitAutoData]
    public async Task UpdateUserAsync_WithFlexibleCollections_WithAccessAll_Throws(
        Organization organization,
        [OrganizationUser(type: OrganizationUserType.User)] OrganizationUser oldUserData,
        [OrganizationUser(type: OrganizationUserType.User)] OrganizationUser newUserData,
        [OrganizationUser(type: OrganizationUserType.Owner, status: OrganizationUserStatusType.Confirmed)] OrganizationUser savingUser,
        ICollection<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups,
        SutProvider<UpdateOrganizationUserCommand> sutProvider)
    {
        organization.FlexibleCollections = true;
        newUserData.Id = oldUserData.Id;
        newUserData.UserId = oldUserData.UserId;
        newUserData.OrganizationId = oldUserData.OrganizationId = savingUser.OrganizationId = organization.Id;
        newUserData.Permissions = CoreHelpers.ClassToJsonData(new Permissions());
        newUserData.AccessAll = true;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IOrganizationService>()
            .HasConfirmedOwnersExceptAsync(
                newUserData.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(i => i.Contains(newUserData.Id)))
            .Returns(true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(oldUserData.Id)
            .Returns(oldUserData);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser> { savingUser });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateUserAsync(newUserData, oldUserData.UserId, collections, groups));

        Assert.Contains("the accessall property has been deprecated", exception.Message.ToLowerInvariant());
    }
}
