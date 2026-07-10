using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Auth.UserFeatures.UserEmail;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
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
            .GetByIdAsync(default);
        await sutProvider.GetDependency<IChangeEmailCommand>()
            .DidNotReceiveWithAnyArgs()
            .ChangeEmailAsync(default, default);
        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushSyncSettingsAsync(default);
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
            .ChangeEmailAsync(default, default);
        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushSyncSettingsAsync(default);
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
            .ReplaceAsync(default, default(IEnumerable<CollectionAccessSelection>));
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(default(OrganizationUser), default);
        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushSyncSettingsAsync(default);
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
        string newEmail = null)
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
            defaultUserCollectionName,
            new StandardUser(organizationUser.UserId ?? Guid.NewGuid(), true),
            new OrganizationUser { Type = OrganizationUserType.Owner });
    }
}
