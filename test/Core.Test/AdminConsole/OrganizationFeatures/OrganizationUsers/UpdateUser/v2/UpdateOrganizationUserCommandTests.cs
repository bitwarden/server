using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Auth.UserFeatures.UserEmail;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using Organization = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;

[SutProviderCustomize]
public class UpdateOrganizationUserCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateUserAsync_WhenEmailChanging_LoadsUserAndCallsChangeEmail(
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        organizationUser.UserId = Guid.NewGuid();
        var userToUpdate = new User { Id = organizationUser.UserId!.Value, Email = "old@claimed.example.com" };
        var request = Setup(sutProvider, organization, organizationUser, newEmail: "new@claimed.example.com");

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(organizationUser.UserId!.Value)
            .Returns(userToUpdate);

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IUserRepository>()
            .Received(1)
            .GetByIdAsync(organizationUser.UserId!.Value);
        await sutProvider.GetDependency<IChangeEmailCommand>()
            .Received(1)
            .ChangeEmailAsync(userToUpdate, "new@claimed.example.com");
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncSettingsAsync(userToUpdate.Id);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ReplaceAsync(organizationUser, Arg.Any<IEnumerable<CollectionAccessSelection>>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateUserAsync_WhenNoEmailRequested_DoesNotLoadUserOrChangeEmail(
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        var request = Setup(sutProvider, organization, organizationUser, newEmail: null);

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IChangeEmailCommand>()
            .DidNotReceiveWithAnyArgs()
            .ChangeEmailAsync(Arg.Any<User>(), Arg.Any<string>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushSyncSettingsAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateUserAsync_WhenEmailUnchanged_DoesNotCallChangeEmail(
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        organizationUser.UserId = Guid.NewGuid();
        var userToUpdate = new User { Id = organizationUser.UserId!.Value, Email = "member@claimed.example.com" };
        var request = Setup(sutProvider, organization, organizationUser, newEmail: "MEMBER@claimed.example.com");

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(organizationUser.UserId!.Value)
            .Returns(userToUpdate);

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IChangeEmailCommand>()
            .DidNotReceiveWithAnyArgs()
            .ChangeEmailAsync(Arg.Any<User>(), Arg.Any<string>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushSyncSettingsAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(ChangeEmailCommand.EmailAlreadyInUseError, typeof(EmailAlreadyInUseError))]
    [BitAutoData(OrganizationDomainAllowEmailChangeQuery.EmailClaimedByOrganizationError, typeof(EmailClaimedByAnotherOrganizationError))]
    [BitAutoData(OrganizationDomainAllowEmailChangeQuery.EmailNotOnVerifiedDomainError, typeof(NewEmailDomainNotClaimedError))]
    [BitAutoData("Something unexpected went wrong.", typeof(EmailChangeFailedError))]
    public async Task UpdateUserAsync_WhenChangeEmailThrowsBadRequest_MapsToTypedErrorAndDoesNotPersist(
        string thrownMessage,
        Type expectedError,
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        organizationUser.UserId = Guid.NewGuid();
        var userToUpdate = new User { Id = organizationUser.UserId!.Value, Email = "old@claimed.example.com" };
        var request = Setup(sutProvider, organization, organizationUser, newEmail: "new@claimed.example.com");

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(organizationUser.UserId!.Value)
            .Returns(userToUpdate);
        sutProvider.GetDependency<IChangeEmailCommand>()
            .ChangeEmailAsync(userToUpdate, "new@claimed.example.com")
            .ThrowsAsync(new BadRequestException(thrownMessage));

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsError);
        Assert.IsType(expectedError, result.AsError);

        // The email change fails before any role/collection changes are persisted.
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<OrganizationUser>(), Arg.Any<IEnumerable<CollectionAccessSelection>>());
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushSyncSettingsAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateUserAsync_WhenNameChanging_LoadsUserPersistsNameAndPushesSync(
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        organizationUser.UserId = Guid.NewGuid();
        var userToUpdate = new User { Id = organizationUser.UserId!.Value, Email = "member@claimed.example.com", Name = "Old Name" };
        var request = Setup(sutProvider, organization, organizationUser, newName: "New Name");

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(organizationUser.UserId!.Value)
            .Returns(userToUpdate);

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", userToUpdate.Name);
        await sutProvider.GetDependency<IUserRepository>()
            .Received(1)
            .ReplaceAsync(userToUpdate);
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncSettingsAsync(userToUpdate.Id);
        // A name-only change never touches the email command.
        await sutProvider.GetDependency<IChangeEmailCommand>()
            .DidNotReceiveWithAnyArgs()
            .ChangeEmailAsync(Arg.Any<User>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateUserAsync_WhenNameBlank_ClearsNameToNull(
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        organizationUser.UserId = Guid.NewGuid();
        var userToUpdate = new User { Id = organizationUser.UserId!.Value, Email = "member@claimed.example.com", Name = "Old Name" };
        var request = Setup(sutProvider, organization, organizationUser, newName: "   ");

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(organizationUser.UserId!.Value)
            .Returns(userToUpdate);

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Null(userToUpdate.Name);
        await sutProvider.GetDependency<IUserRepository>()
            .Received(1)
            .ReplaceAsync(userToUpdate);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateUserAsync_WhenNameUnchanged_DoesNotPersistUserOrPushSync(
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        organizationUser.UserId = Guid.NewGuid();
        var userToUpdate = new User { Id = organizationUser.UserId!.Value, Email = "member@claimed.example.com", Name = "Same Name" };
        var request = Setup(sutProvider, organization, organizationUser, newName: "Same Name");

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(organizationUser.UserId!.Value)
            .Returns(userToUpdate);

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushSyncSettingsAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateUserAsync_WhenNameNull_DoesNotLoadUserOrPersistName(
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        var request = Setup(sutProvider, organization, organizationUser, newName: null);

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<User>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateUserAsync_WhenNameAndEmailChanging_WritesAccountOnceViaChangeEmailAndPushesOnce(
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        organizationUser.UserId = Guid.NewGuid();
        var userToUpdate = new User { Id = organizationUser.UserId!.Value, Email = "old@claimed.example.com", Name = "Old Name" };
        var request = Setup(sutProvider, organization, organizationUser,
            newEmail: "new@claimed.example.com", newName: "New Name");

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(organizationUser.UserId!.Value)
            .Returns(userToUpdate);

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", userToUpdate.Name);
        // The email command persists the account (name included); we must not also call ReplaceAsync directly.
        await sutProvider.GetDependency<IChangeEmailCommand>()
            .Received(1)
            .ChangeEmailAsync(userToUpdate, "new@claimed.example.com");
        await sutProvider.GetDependency<IUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncSettingsAsync(userToUpdate.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateUserAsync_WhenNameRequestedButMemberHasNoAccount_SkipsNameChange(
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Invited, OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        organizationUser.UserId = null;
        var request = Setup(sutProvider, organization, organizationUser, newName: "New Name");

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushSyncSettingsAsync(Arg.Any<Guid>());
    }

    private static UpdateOrganizationUserRequest Setup(
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        OrganizationUser organizationUser,
        OrganizationUserType type = OrganizationUserType.User,
        List<CollectionAccessSelection> collections = null,
        IEnumerable<Guid> groups = null,
        bool valid = true,
        bool targetAccessSecretsManager = false,
        string defaultUserCollectionName = null,
        string newEmail = null,
        string newName = null)
    {
        organization.PlanType = PlanType.EnterpriseAnnually;
        organizationUser.OrganizationId = organization.Id;

        sutProvider.GetDependency<IUpdateOrganizationUserValidator>()
            .ValidateAsync(Arg.Any<UpdateOrganizationUserRequest>())
            .Returns(ci => valid
                ? ValidationResultHelpers.Valid(ci.Arg<UpdateOrganizationUserRequest>())
                : ValidationResultHelpers.Invalid(ci.Arg<UpdateOrganizationUserRequest>(), new MustHaveConfirmedOwner()));

        return new UpdateOrganizationUserRequest(
            organizationUser,
            organization,
            [],
            [],
            type,
            null,
            targetAccessSecretsManager,
            collections,
            groups,
            newEmail,
            newName,
            defaultUserCollectionName,
            new StandardUser(organizationUser.UserId ?? Guid.NewGuid(), true),
            new OrganizationUser { Type = OrganizationUserType.Owner });
    }
}
