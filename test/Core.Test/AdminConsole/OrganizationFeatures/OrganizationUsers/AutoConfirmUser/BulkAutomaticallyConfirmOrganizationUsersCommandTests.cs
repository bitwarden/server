using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationConfirmation;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

[SutProviderCustomize]
public class BulkAutomaticallyConfirmOrganizationUsersCommandTests
{
    [Theory, BitAutoData]
    public async Task BulkAutomaticallyConfirmOrganizationUsersAsync_ConfirmedUsers_LogsEventForEach(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser2,
        string key1,
        string key2,
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersCommand> sutProvider)
    {
        // Arrange
        orgUser1.OrganizationId = organization.Id;
        orgUser2.OrganizationId = organization.Id;

        var request = BuildRequest(organization,
            (orgUser1.Id, key1),
            (orgUser2.Id, key2));

        SetupCommonMocks(sutProvider, [orgUser1, orgUser2]);
        SetupValidatorAllValid(sutProvider, request, [orgUser1, orgUser2], organization);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmManyOrganizationUsersAsync(Arg.Any<IEnumerable<AcceptedOrganizationUserToConfirm>>())
            .Returns([orgUser1.Id, orgUser2.Id]);

        // Act
        await sutProvider.Sut.RunAsync(request);

        // Assert — one per-user event call for each confirmed user
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(orgUser1, EventType.OrganizationUser_AutomaticallyConfirmed, Arg.Any<DateTime?>());

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(orgUser2, EventType.OrganizationUser_AutomaticallyConfirmed, Arg.Any<DateTime?>());
    }

    [Theory, BitAutoData]
    public async Task BulkAutomaticallyConfirmOrganizationUsersAsync_ConfirmedUsers_SendsConfirmationEmailForEach(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser2,
        User user1,
        User user2,
        string key1,
        string key2,
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersCommand> sutProvider)
    {
        // Arrange
        orgUser1.OrganizationId = organization.Id;
        orgUser2.OrganizationId = organization.Id;
        user1.Id = orgUser1.UserId!.Value;
        user2.Id = orgUser2.UserId!.Value;

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([user1, user2]);

        var request = BuildRequest(organization,
            (orgUser1.Id, key1),
            (orgUser2.Id, key2));

        SetupCommonMocks(sutProvider, [orgUser1, orgUser2]);
        SetupValidatorAllValid(sutProvider, request, [orgUser1, orgUser2], organization);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmManyOrganizationUsersAsync(Arg.Any<IEnumerable<AcceptedOrganizationUserToConfirm>>())
            .Returns([orgUser1.Id, orgUser2.Id]);

        // Act
        await sutProvider.Sut.RunAsync(request);

        // Assert — confirmation email sent to each confirmed user
        await sutProvider.GetDependency<ISendOrganizationConfirmationCommand>()
            .Received(1)
            .SendConfirmationAsync(organization, user1.Email, orgUser1.AccessSecretsManager);

        await sutProvider.GetDependency<ISendOrganizationConfirmationCommand>()
            .Received(1)
            .SendConfirmationAsync(organization, user2.Email, orgUser2.AccessSecretsManager);
    }

    [Theory, BitAutoData]
    public async Task BulkAutomaticallyConfirmOrganizationUsersAsync_ConfirmedUsers_SyncsOrgKeysAndDeletesDeviceRegistrationForEach(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser2,
        Device device1,
        Device device2,
        string key1,
        string key2,
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersCommand> sutProvider)
    {
        // Arrange
        orgUser1.OrganizationId = organization.Id;
        orgUser2.OrganizationId = organization.Id;

        device1.PushToken = "push-token-1";
        device2.PushToken = "push-token-2";

        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(orgUser1.UserId!.Value)
            .Returns([device1]);
        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(orgUser2.UserId!.Value)
            .Returns([device2]);

        var request = BuildRequest(organization,
            (orgUser1.Id, key1),
            (orgUser2.Id, key2));

        SetupCommonMocks(sutProvider, [orgUser1, orgUser2]);
        SetupValidatorAllValid(sutProvider, request, [orgUser1, orgUser2], organization);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmManyOrganizationUsersAsync(Arg.Any<IEnumerable<AcceptedOrganizationUserToConfirm>>())
            .Returns([orgUser1.Id, orgUser2.Id]);

        // Act
        await sutProvider.Sut.RunAsync(request);

        // Assert — org-key push notification sent for each confirmed user
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncOrgKeysAsync(orgUser1.UserId!.Value);

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncOrgKeysAsync(orgUser2.UserId!.Value);

        // Assert — device registrations deleted per confirmed user
        await sutProvider.GetDependency<IPushRegistrationService>()
            .Received(1)
            .DeleteUserRegistrationOrganizationAsync(
                Arg.Is<IEnumerable<string>>(ids => ids.Single() == device1.Id.ToString()),
                organization.Id.ToString());

        await sutProvider.GetDependency<IPushRegistrationService>()
            .Received(1)
            .DeleteUserRegistrationOrganizationAsync(
                Arg.Is<IEnumerable<string>>(ids => ids.Single() == device2.Id.ToString()),
                organization.Id.ToString());
    }

    [Theory, BitAutoData]
    public async Task BulkAutomaticallyConfirmOrganizationUsersAsync_AllValidationFails_DoesNotRunSideEffects(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser2,
        string key1,
        string key2,
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersCommand> sutProvider)
    {
        // Arrange
        orgUser1.OrganizationId = organization.Id;
        orgUser2.OrganizationId = organization.Id;

        var request = BuildRequest(organization,
            (orgUser1.Id, key1),
            (orgUser2.Id, key2));

        SetupCommonMocks(sutProvider, [orgUser1, orgUser2]);

        sutProvider.GetDependency<IBulkAutomaticallyConfirmOrganizationUsersValidator>()
            .ValidateManyAsync(Arg.Any<IEnumerable<AutomaticallyConfirmOrganizationUserValidationRequest>>(), Arg.Any<Organization>())
            .Returns([
                ValidationResultHelpers.Invalid(BuildValidationRequest(request.UsersToConfirm[0], request, orgUser1, organization), new UserIsNotAccepted()),
                ValidationResultHelpers.Invalid(BuildValidationRequest(request.UsersToConfirm[1], request, orgUser2, organization), new UserIsNotAccepted())
            ]);

        // Act
        await sutProvider.Sut.RunAsync(request);

        // Assert — no side effects fired when no users were confirmed
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceive()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<DateTime?>());

        await sutProvider.GetDependency<ISendOrganizationConfirmationCommand>()
            .DidNotReceive()
            .SendConfirmationAsync(Arg.Any<Organization>(), Arg.Any<string>(), Arg.Any<bool>());

        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceive()
            .PushSyncOrgKeysAsync(Arg.Any<Guid>());

        await sutProvider.GetDependency<IPushRegistrationService>()
            .DidNotReceive()
            .DeleteUserRegistrationOrganizationAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task BulkAutomaticallyConfirmOrganizationUsersAsync_OneSucceedsOneFails_RunsSideEffectsOnlyForConfirmedUser(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser2,
        User user1,
        string key1,
        string key2,
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersCommand> sutProvider)
    {
        // Arrange
        orgUser1.OrganizationId = organization.Id;
        orgUser2.OrganizationId = organization.Id;
        user1.Id = orgUser1.UserId!.Value;

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([user1]);

        var request = BuildRequest(organization,
            (orgUser1.Id, key1),
            (orgUser2.Id, key2));

        SetupCommonMocks(sutProvider, [orgUser1, orgUser2]);

        // orgUser1 passes validation; orgUser2 fails
        sutProvider.GetDependency<IBulkAutomaticallyConfirmOrganizationUsersValidator>()
            .ValidateManyAsync(Arg.Any<IEnumerable<AutomaticallyConfirmOrganizationUserValidationRequest>>(), Arg.Any<Organization>())
            .Returns([
                ValidationResultHelpers.Valid(BuildValidationRequest(request.UsersToConfirm[0], request, orgUser1, organization)),
                ValidationResultHelpers.Invalid(BuildValidationRequest(request.UsersToConfirm[1], request, orgUser2, organization), new UserIsNotAccepted())
            ]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmManyOrganizationUsersAsync(Arg.Any<IEnumerable<AcceptedOrganizationUserToConfirm>>())
            .Returns([orgUser1.Id]);

        // Act
        await sutProvider.Sut.RunAsync(request);

        // Assert — event logged only for orgUser1
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(orgUser1, EventType.OrganizationUser_AutomaticallyConfirmed, Arg.Any<DateTime?>());

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceive()
            .LogOrganizationUserEventAsync(orgUser2, Arg.Any<EventType>(), Arg.Any<DateTime?>());

        await sutProvider.GetDependency<ISendOrganizationConfirmationCommand>()
            .Received(1)
            .SendConfirmationAsync(organization, user1.Email, orgUser1.AccessSecretsManager);

        await sutProvider.GetDependency<ISendOrganizationConfirmationCommand>()
            .DidNotReceive()
            .SendConfirmationAsync(organization, Arg.Is<string>(e => e != user1.Email), Arg.Any<bool>());

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncOrgKeysAsync(orgUser1.UserId!.Value);

        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceive()
            .PushSyncOrgKeysAsync(orgUser2.UserId!.Value);
    }

    [Theory, BitAutoData]
    public async Task BulkAutomaticallyConfirmOrganizationUsersAsync_EmptyList_DoesNotValidate(
        Organization organization,
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersCommand> sutProvider)
    {
        // Act
        await sutProvider.Sut.RunAsync(BuildRequest(organization));

        // Assert
        await sutProvider.GetDependency<IBulkAutomaticallyConfirmOrganizationUsersValidator>()
            .DidNotReceive()
            .ValidateManyAsync(Arg.Any<IEnumerable<AutomaticallyConfirmOrganizationUserValidationRequest>>(), Arg.Any<Organization>());
    }

    private static BulkAutomaticallyConfirmOrganizationUsersRequest BuildRequest(
        Organization organization,
        params (Guid OrgUserId, string Key)[] userKeys) =>
        new()
        {
            Organization = organization,
            DefaultUserCollectionName = string.Empty,
            UsersToConfirm = userKeys
                .Select(u => new BulkAutoConfirmUserEntry { OrganizationUserId = u.OrgUserId, Key = u.Key })
                .ToList(),
        };

    private static AutomaticallyConfirmOrganizationUserValidationRequest BuildValidationRequest(
        BulkAutoConfirmUserEntry entry,
        BulkAutomaticallyConfirmOrganizationUsersRequest request,
        OrganizationUser orgUser,
        Organization organization) =>
        new()
        {
            Key = entry.Key,
            DefaultUserCollectionName = request.DefaultUserCollectionName,
            OrganizationUserId = orgUser.Id,
            OrganizationId = organization.Id,
            OrganizationUser = orgUser,
            Organization = organization
        };

    private static void SetupCommonMocks(
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersCommand> sutProvider,
        ICollection<OrganizationUser> orgUsers)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(orgUsers);
    }

    private static void SetupValidatorAllValid(
        SutProvider<BulkAutomaticallyConfirmOrganizationUsersCommand> sutProvider,
        BulkAutomaticallyConfirmOrganizationUsersRequest request,
        ICollection<OrganizationUser> orgUsers,
        Organization organization)
    {
        var orgUserById = orgUsers.ToDictionary(ou => ou.Id);
        var validationResults = request.UsersToConfirm
            .Select(u => ValidationResultHelpers.Valid(BuildValidationRequest(u, request, orgUserById[u.OrganizationUserId], organization)))
            .ToList<ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest>>();

        sutProvider.GetDependency<IBulkAutomaticallyConfirmOrganizationUsersValidator>()
            .ValidateManyAsync(Arg.Any<IEnumerable<AutomaticallyConfirmOrganizationUserValidationRequest>>(), Arg.Any<Organization>())
            .Returns(validationResults);
    }
}
