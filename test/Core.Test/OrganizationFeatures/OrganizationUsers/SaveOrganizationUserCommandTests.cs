using System.Text.Json;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class SaveOrganizationUserCommandTests
{
    [Theory, BitAutoData]
    public async Task SaveUser_NoUserId_Throws(OrganizationUser user, Guid? savingUserId,
        IEnumerable<CollectionAccessSelection> collections, IEnumerable<Guid> groups, SutProvider<SaveOrganizationUserCommand> sutProvider)
    {
        user.Id = default(Guid);
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveUserAsync(user, savingUserId, collections, groups));
        Assert.Contains("invite the user first", exception.Message.ToLowerInvariant());
    }

    [Theory, BitAutoData]
    public async Task SaveUser_NoChangeToData_Throws(OrganizationUser user, Guid? savingUserId,
        IEnumerable<CollectionAccessSelection> collections, IEnumerable<Guid> groups, SutProvider<SaveOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        organizationUserRepository.GetByIdAsync(user.Id).Returns(user);
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveUserAsync(user, savingUserId, collections, groups));
        Assert.Contains("make changes before saving", exception.Message.ToLowerInvariant());
    }

    [Theory, BitAutoData]
    public async Task SaveUser_Passes(
        Organization organization,
        OrganizationUser oldUserData,
        OrganizationUser newUserData,
        IEnumerable<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups,
        Permissions permissions,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser savingUser,
        SutProvider<SaveOrganizationUserCommand> sutProvider)
    {
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();
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
        currentContext.OrganizationOwner(savingUser.OrganizationId).Returns(true);
        organizationService.HasConfirmedOwnersExceptAsync(organization.Id, Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(newUserData.Id))).Returns(true);

        await sutProvider.Sut.SaveUserAsync(newUserData, savingUser.UserId, collections, groups);
    }

    [Theory, BitAutoData]
    public async Task SaveUser_WithCustomType_WhenUseCustomPermissionsIsFalse_Throws(
        Organization organization,
        OrganizationUser oldUserData,
        [OrganizationUser(type: OrganizationUserType.Custom)] OrganizationUser newUserData,
        IEnumerable<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser savingUser,
        SutProvider<SaveOrganizationUserCommand> sutProvider)
    {
        organization.UseCustomPermissions = false;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        newUserData.Id = oldUserData.Id;
        newUserData.UserId = oldUserData.UserId;
        newUserData.OrganizationId = savingUser.OrganizationId = oldUserData.OrganizationId = organization.Id;
        newUserData.Permissions = null;
        organizationUserRepository.GetByIdAsync(oldUserData.Id).Returns(oldUserData);
        organizationUserRepository.GetManyByOrganizationAsync(savingUser.OrganizationId, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser> { savingUser });
        currentContext.OrganizationOwner(savingUser.OrganizationId).Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveUserAsync(newUserData, savingUser.UserId, collections, groups));
        Assert.Contains("to enable custom permissions", exception.Message.ToLowerInvariant());
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Manager)]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.User)]
    public async Task SaveUser_WithNonCustomType_WhenUseCustomPermissionsIsFalse_Passes(
        OrganizationUserType newUserType,
        Organization organization,
        OrganizationUser oldUserData,
        OrganizationUser newUserData,
        IEnumerable<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups,
        Permissions permissions,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser savingUser,
        SutProvider<SaveOrganizationUserCommand> sutProvider)
    {
        organization.UseCustomPermissions = false;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        newUserData.Id = oldUserData.Id;
        newUserData.UserId = oldUserData.UserId;
        newUserData.OrganizationId = savingUser.OrganizationId = oldUserData.OrganizationId = organization.Id;
        newUserData.Type = newUserType;
        newUserData.Permissions = JsonSerializer.Serialize(permissions, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        organizationUserRepository.GetByIdAsync(oldUserData.Id).Returns(oldUserData);
        organizationUserRepository.GetManyByOrganizationAsync(savingUser.OrganizationId, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser> { savingUser });
        currentContext.OrganizationOwner(savingUser.OrganizationId).Returns(true);
        organizationService.HasConfirmedOwnersExceptAsync(organization.Id, Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(newUserData.Id))).Returns(true);

        await sutProvider.Sut.SaveUserAsync(newUserData, savingUser.UserId, collections, groups);
    }

    [Theory, BitAutoData]
    public async Task SaveUser_WithCustomType_WhenUseCustomPermissionsIsTrue_Passes(
        Organization organization,
        OrganizationUser oldUserData,
        [OrganizationUser(type: OrganizationUserType.Custom)] OrganizationUser newUserData,
        IEnumerable<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups,
        Permissions permissions,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser savingUser,
        SutProvider<SaveOrganizationUserCommand> sutProvider)
    {
        organization.UseCustomPermissions = true;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();
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
        currentContext.OrganizationOwner(savingUser.OrganizationId).Returns(true);
        organizationService.HasConfirmedOwnersExceptAsync(organization.Id, Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(newUserData.Id))).Returns(true);

        await sutProvider.Sut.SaveUserAsync(newUserData, savingUser.UserId, collections, groups);
    }

    [Theory, BitAutoData]
    public async Task SaveUser_WithCustomPermission_WhenSavingUserHasCustomPermission_Passes(
        Organization organization,
        [OrganizationUser(type: OrganizationUserType.User)] OrganizationUser oldUserData,
        [OrganizationUser(type: OrganizationUserType.Custom)] OrganizationUser newUserData,
        IEnumerable<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups,
        [OrganizationUser(type: OrganizationUserType.Custom)] OrganizationUser savingUser,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser organizationOwner,
        SutProvider<SaveOrganizationUserCommand> sutProvider)
    {
        organization.UseCustomPermissions = true;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        newUserData.Id = oldUserData.Id;
        newUserData.UserId = oldUserData.UserId;
        newUserData.OrganizationId = savingUser.OrganizationId = oldUserData.OrganizationId = organizationOwner.OrganizationId = organization.Id;
        newUserData.Permissions = JsonSerializer.Serialize(new Permissions { AccessReports = true }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        organizationUserRepository.GetByIdAsync(oldUserData.Id).Returns(oldUserData);
        organizationUserRepository.GetManyByOrganizationAsync(savingUser.OrganizationId, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser> { organizationOwner });
        currentContext.OrganizationCustom(savingUser.OrganizationId).Returns(true);
        currentContext.ManageUsers(savingUser.OrganizationId).Returns(true);
        currentContext.AccessReports(savingUser.OrganizationId).Returns(true);
        organizationService.HasConfirmedOwnersExceptAsync(organization.Id, Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(newUserData.Id))).Returns(true);

        await sutProvider.Sut.SaveUserAsync(newUserData, savingUser.UserId, collections, groups);
    }

    [Theory, BitAutoData]
    public async Task SaveUser_WithCustomPermission_WhenSavingUserDoesNotHaveCustomPermission_Throws(
        Organization organization,
        [OrganizationUser(type: OrganizationUserType.User)] OrganizationUser oldUserData,
        [OrganizationUser(type: OrganizationUserType.Custom)] OrganizationUser newUserData,
        IEnumerable<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups,
        [OrganizationUser(type: OrganizationUserType.Custom)] OrganizationUser savingUser,
        SutProvider<SaveOrganizationUserCommand> sutProvider)
    {
        organization.UseCustomPermissions = true;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        newUserData.Id = oldUserData.Id;
        newUserData.UserId = oldUserData.UserId;
        newUserData.OrganizationId = savingUser.OrganizationId = oldUserData.OrganizationId = organization.Id;
        newUserData.Permissions = JsonSerializer.Serialize(new Permissions { AccessReports = true }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        organizationUserRepository.GetByIdAsync(oldUserData.Id).Returns(oldUserData);
        currentContext.OrganizationCustom(savingUser.OrganizationId).Returns(true);
        currentContext.ManageUsers(savingUser.OrganizationId).Returns(true);
        currentContext.AccessReports(savingUser.OrganizationId).Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveUserAsync(newUserData, savingUser.UserId, collections, groups));
        Assert.Contains("custom users can only grant the same custom permissions that they have", exception.Message.ToLowerInvariant());
    }

    [Theory, BitAutoData]
    public async Task SaveUser_WithCustomPermission_WhenUpgradingToAdmin_Throws(
        Organization organization,
        [OrganizationUser(type: OrganizationUserType.Custom)] OrganizationUser oldUserData,
        [OrganizationUser(type: OrganizationUserType.Admin)] OrganizationUser newUserData,
        IEnumerable<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups,
        SutProvider<SaveOrganizationUserCommand> sutProvider)
    {
        organization.UseCustomPermissions = true;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        newUserData.Id = oldUserData.Id;
        newUserData.UserId = oldUserData.UserId;
        newUserData.OrganizationId = oldUserData.OrganizationId = organization.Id;
        newUserData.Permissions = JsonSerializer.Serialize(new Permissions { AccessReports = true }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        organizationUserRepository.GetByIdAsync(oldUserData.Id).Returns(oldUserData);
        currentContext.OrganizationCustom(oldUserData.OrganizationId).Returns(true);
        currentContext.ManageUsers(oldUserData.OrganizationId).Returns(true);
        currentContext.AccessReports(oldUserData.OrganizationId).Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveUserAsync(newUserData, oldUserData.UserId, collections, groups));
        Assert.Contains("custom users can not manage admins or owners", exception.Message.ToLowerInvariant());
    }
}
