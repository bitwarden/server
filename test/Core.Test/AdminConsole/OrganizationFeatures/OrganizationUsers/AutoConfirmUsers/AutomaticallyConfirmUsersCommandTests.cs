using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Models.Data.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUsers;

[SutProviderCustomize]
public class AutomaticallyConfirmUsersCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task AutomaticallyConfirmOrganizationUserAsync_WithValidRequest_ConfirmsUserSuccessfully(
        SutProvider<AutomaticallyConfirmOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        User user,
        Guid performingUserId,
        string key,
        string defaultCollectionName)
    {
        // Arrange
        organizationUser.UserId = user.Id;
        organizationUser.OrganizationId = organization.Id;

        var request = new AutomaticallyConfirmOrganizationUserRequest
        {
            OrganizationUserId = organizationUser.Id,
            OrganizationId = organization.Id,
            Key = key,
            DefaultUserCollectionName = defaultCollectionName,
            PerformedBy = new StandardUser(performingUserId, true)
        };

        SetupRepositoryMocks(sutProvider, organizationUser, organization, user);
        SetupValidatorMock(sutProvider, request, organizationUser, organization, true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmOrganizationUserAsync(Arg.Is<AcceptedOrganizationUserToConfirm>(o =>
                o.OrganizationUserId == organizationUser.Id && o.Key == request.Key))
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.AutomaticallyConfirmOrganizationUserAsync(request);

        // Assert
        Assert.True(result.IsSuccess);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ConfirmOrganizationUserAsync(Arg.Is<AcceptedOrganizationUserToConfirm>(o =>
                o.OrganizationUserId == organizationUser.Id && o.Key == request.Key));

        await AssertSuccessfulOperationsAsync(sutProvider, organizationUser, organization, user, key);
    }

    [Theory]
    [BitAutoData]
    public async Task AutomaticallyConfirmOrganizationUserAsync_WithInvalidUserOrgId_ReturnsOrganizationUserIdIsInvalidError(
        SutProvider<AutomaticallyConfirmOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        User user,
        Guid performingUserId,
        string key,
        string defaultCollectionName)
    {
        // Arrange
        organizationUser.UserId = user.Id;
        organizationUser.OrganizationId = Guid.NewGuid(); // User belongs to another organization
        var request = new AutomaticallyConfirmOrganizationUserRequest
        {
            OrganizationUserId = organizationUser.Id,
            OrganizationId = organization.Id,
            Key = key,
            DefaultUserCollectionName = defaultCollectionName,
            PerformedBy = new StandardUser(performingUserId, true)
        };

        SetupRepositoryMocks(sutProvider, organizationUser, organization, user);
        SetupValidatorMock(sutProvider, request, organizationUser, organization, false, new OrganizationUserIdIsInvalid());

        // Act
        var result = await sutProvider.Sut.AutomaticallyConfirmOrganizationUserAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationUserIdIsInvalid>(result.AsError);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceive()
            .ConfirmOrganizationUserAsync(Arg.Any<AcceptedOrganizationUserToConfirm>());
    }

    [Theory]
    [BitAutoData]
    public async Task AutomaticallyConfirmOrganizationUserAsync_WhenAlreadyConfirmed_ReturnsNoneSuccess(
        SutProvider<AutomaticallyConfirmOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        User user,
        Guid performingUserId,
        string key,
        string defaultCollectionName)
    {
        // Arrange
        organizationUser.UserId = user.Id;
        organizationUser.OrganizationId = organization.Id;
        var request = new AutomaticallyConfirmOrganizationUserRequest
        {
            OrganizationUserId = organizationUser.Id,
            OrganizationId = organization.Id,
            Key = key,
            DefaultUserCollectionName = defaultCollectionName,
            PerformedBy = new StandardUser(performingUserId, true)
        };

        SetupRepositoryMocks(sutProvider, organizationUser, organization, user);
        SetupValidatorMock(sutProvider, request, organizationUser, organization, true);

        // Return false to indicate the user is already confirmed
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmOrganizationUserAsync(Arg.Is<AcceptedOrganizationUserToConfirm>(x =>
                x.OrganizationUserId == organizationUser.Id && x.Key == request.Key))
            .Returns(false);

        // Act
        var result = await sutProvider.Sut.AutomaticallyConfirmOrganizationUserAsync(request);

        // Assert
        Assert.True(result.IsSuccess);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ConfirmOrganizationUserAsync(Arg.Is<AcceptedOrganizationUserToConfirm>(x =>
                x.OrganizationUserId == organizationUser.Id && x.Key == request.Key));

        // Verify no side effects occurred
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceive()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<DateTime?>());

        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceive()
            .PushSyncOrgKeysAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task AutomaticallyConfirmOrganizationUserAsync_WithDefaultCollectionEnabled_CreatesDefaultCollection(
        SutProvider<AutomaticallyConfirmOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        User user,
        Guid performingUserId,
        string key,
        string defaultCollectionName)
    {
        // Arrange
        organizationUser.UserId = user.Id;
        organizationUser.OrganizationId = organization.Id;
        var request = new AutomaticallyConfirmOrganizationUserRequest
        {
            OrganizationUserId = organizationUser.Id,
            OrganizationId = organization.Id,
            Key = key,
            DefaultUserCollectionName = defaultCollectionName, // Non-empty to trigger creation
            PerformedBy = new StandardUser(performingUserId, true)
        };

        SetupRepositoryMocks(sutProvider, organizationUser, organization, user);
        SetupValidatorMock(sutProvider, request, organizationUser, organization, true);
        SetupPolicyRequirementMock(sutProvider, user.Id, organization.Id, true); // Policy requires collection

        sutProvider.GetDependency<IOrganizationUserRepository>().ConfirmOrganizationUserAsync(
                Arg.Is<AcceptedOrganizationUserToConfirm>(o =>
                    o.OrganizationUserId == organizationUser.Id && o.Key == request.Key))
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.AutomaticallyConfirmOrganizationUserAsync(request);

        // Assert
        Assert.True(result.IsSuccess);

        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .CreateAsync(
                Arg.Is<Collection>(c =>
                    c.OrganizationId == organization.Id &&
                    c.Name == defaultCollectionName &&
                    c.Type == CollectionType.DefaultUserCollection),
                Arg.Is<IEnumerable<CollectionAccessSelection>>(groups => groups == null),
                Arg.Is<IEnumerable<CollectionAccessSelection>>(access =>
                    access.FirstOrDefault(x => x.Id == organizationUser.Id && x.Manage) != null));
    }

    [Theory]
    [BitAutoData]
    public async Task AutomaticallyConfirmOrganizationUserAsync_WithDefaultCollectionDisabled_DoesNotCreateCollection(
        SutProvider<AutomaticallyConfirmOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        User user,
        Guid performingUserId,
        string key)
    {
        // Arrange
        organizationUser.UserId = user.Id;
        organizationUser.OrganizationId = organization.Id;
        var request = new AutomaticallyConfirmOrganizationUserRequest
        {
            OrganizationUserId = organizationUser.Id,
            OrganizationId = organization.Id,
            Key = key,
            DefaultUserCollectionName = string.Empty, // Empty, so the collection won't be created
            PerformedBy = new StandardUser(performingUserId, true)
        };

        SetupRepositoryMocks(sutProvider, organizationUser, organization, user);
        SetupValidatorMock(sutProvider, request, organizationUser, organization, true);
        SetupPolicyRequirementMock(sutProvider, user.Id, organization.Id, false); // Policy doesn't require

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmOrganizationUserAsync(Arg.Is<AcceptedOrganizationUserToConfirm>(o =>
                o.OrganizationUserId == organizationUser.Id && o.Key == request.Key))
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.AutomaticallyConfirmOrganizationUserAsync(request);

        // Assert
        Assert.True(result.IsSuccess);

        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .CreateAsync(Arg.Any<Collection>(),
                Arg.Any<IEnumerable<CollectionAccessSelection>>(),
                Arg.Any<IEnumerable<CollectionAccessSelection>>());
    }

    [Theory]
    [BitAutoData]
    public async Task AutomaticallyConfirmOrganizationUserAsync_WhenCreateDefaultCollectionFails_LogsErrorButReturnsSuccess(
        SutProvider<AutomaticallyConfirmOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        User user,
        Guid performingUserId,
        string key,
        string defaultCollectionName)
    {
        // Arrange
        organizationUser.UserId = user.Id;
        organizationUser.OrganizationId = organization.Id;
        var request = new AutomaticallyConfirmOrganizationUserRequest
        {
            OrganizationUserId = organizationUser.Id,
            OrganizationId = organization.Id,
            Key = key,
            DefaultUserCollectionName = defaultCollectionName, // Non-empty to trigger creation
            PerformedBy = new StandardUser(performingUserId, true)
        };

        SetupRepositoryMocks(sutProvider, organizationUser, organization, user);
        SetupValidatorMock(sutProvider, request, organizationUser, organization, true);
        SetupPolicyRequirementMock(sutProvider, user.Id, organization.Id, true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmOrganizationUserAsync(Arg.Is<AcceptedOrganizationUserToConfirm>(o =>
                o.OrganizationUserId == organizationUser.Id && o.Key == request.Key)).Returns(true);

        var collectionException = new Exception("Collection creation failed");
        sutProvider.GetDependency<ICollectionRepository>()
            .CreateAsync(Arg.Any<Collection>(),
                Arg.Any<IEnumerable<CollectionAccessSelection>>(),
                Arg.Any<IEnumerable<CollectionAccessSelection>>())
            .ThrowsAsync(collectionException);

        // Act
        var result = await sutProvider.Sut.AutomaticallyConfirmOrganizationUserAsync(request);

        // Assert - side effects are fire-and-forget, so command returns success even if collection creation fails
        Assert.True(result.IsSuccess);

        sutProvider.GetDependency<ILogger<AutomaticallyConfirmOrganizationUserCommand>>()
            .Received(1)
            .Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("Failed to create default collection for user")),
                collectionException,
                Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [BitAutoData]
    public async Task AutomaticallyConfirmOrganizationUserAsync_WhenEventLogFails_LogsErrorButReturnsSuccess(
        SutProvider<AutomaticallyConfirmOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        User user,
        Guid performingUserId,
        string key,
        string defaultCollectionName)
    {
        // Arrange
        organizationUser.UserId = user.Id;
        organizationUser.OrganizationId = organization.Id;
        var request = new AutomaticallyConfirmOrganizationUserRequest
        {
            OrganizationUserId = organizationUser.Id,
            OrganizationId = organization.Id,
            Key = key,
            DefaultUserCollectionName = defaultCollectionName,
            PerformedBy = new StandardUser(performingUserId, true)
        };

        SetupRepositoryMocks(sutProvider, organizationUser, organization, user);
        SetupValidatorMock(sutProvider, request, organizationUser, organization, true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmOrganizationUserAsync(Arg.Is<AcceptedOrganizationUserToConfirm>(o =>
                o.OrganizationUserId == organizationUser.Id && o.Key == request.Key))
            .Returns(true);

        var eventException = new Exception("Event logging failed");
        sutProvider.GetDependency<IEventService>()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(),
                EventType.OrganizationUser_AutomaticallyConfirmed,
                Arg.Any<DateTime?>())
            .ThrowsAsync(eventException);

        // Act
        var result = await sutProvider.Sut.AutomaticallyConfirmOrganizationUserAsync(request);

        // Assert - side effects are fire-and-forget, so command returns success even if event log fails
        Assert.True(result.IsSuccess);

        sutProvider.GetDependency<ILogger<AutomaticallyConfirmOrganizationUserCommand>>()
            .Received(1)
            .Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("Failed to log OrganizationUser_AutomaticallyConfirmed event")),
                eventException,
                Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [BitAutoData]
    public async Task AutomaticallyConfirmOrganizationUserAsync_WhenSendEmailFails_LogsErrorButReturnsSuccess(
        SutProvider<AutomaticallyConfirmOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        User user,
        Guid performingUserId,
        string key,
        string defaultCollectionName)
    {
        // Arrange
        organizationUser.UserId = user.Id;
        organizationUser.OrganizationId = organization.Id;
        var request = new AutomaticallyConfirmOrganizationUserRequest
        {
            OrganizationUserId = organizationUser.Id,
            OrganizationId = organization.Id,
            Key = key,
            DefaultUserCollectionName = defaultCollectionName,
            PerformedBy = new StandardUser(performingUserId, true)
        };

        SetupRepositoryMocks(sutProvider, organizationUser, organization, user);
        SetupValidatorMock(sutProvider, request, organizationUser, organization, true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmOrganizationUserAsync(Arg.Is<AcceptedOrganizationUserToConfirm>(o =>
                o.OrganizationUserId == organizationUser.Id && o.Key == request.Key))
            .Returns(true);

        var emailException = new Exception("Email sending failed");
        sutProvider.GetDependency<IMailService>()
            .SendOrganizationConfirmedEmailAsync(organization.Name, user.Email, organizationUser.AccessSecretsManager)
            .ThrowsAsync(emailException);

        // Act
        var result = await sutProvider.Sut.AutomaticallyConfirmOrganizationUserAsync(request);

        // Assert - side effects are fire-and-forget, so command returns success even if email fails
        Assert.True(result.IsSuccess);

        sutProvider.GetDependency<ILogger<AutomaticallyConfirmOrganizationUserCommand>>()
            .Received(1)
            .Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("Failed to send OrganizationUserConfirmed")),
                emailException,
                Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [BitAutoData]
    public async Task AutomaticallyConfirmOrganizationUserAsync_WhenUserNotFoundForEmail_LogsErrorButReturnsSuccess(
        SutProvider<AutomaticallyConfirmOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        User user,
        Guid performingUserId,
        string key,
        string defaultCollectionName)
    {
        // Arrange
        organizationUser.UserId = user.Id;
        organizationUser.OrganizationId = organization.Id;
        var request = new AutomaticallyConfirmOrganizationUserRequest
        {
            OrganizationUserId = organizationUser.Id,
            OrganizationId = organization.Id,
            Key = key,
            DefaultUserCollectionName = defaultCollectionName,
            PerformedBy = new StandardUser(performingUserId, true)
        };

        SetupRepositoryMocks(sutProvider, organizationUser, organization, user);
        SetupValidatorMock(sutProvider, request, organizationUser, organization, true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmOrganizationUserAsync(Arg.Is<AcceptedOrganizationUserToConfirm>(o =>
                o.OrganizationUserId == organizationUser.Id && o.Key == request.Key))
            .Returns(true);

        // Return null when retrieving user for email
        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(user.Id)
            .Returns((User)null!);

        // Act
        var result = await sutProvider.Sut.AutomaticallyConfirmOrganizationUserAsync(request);

        // Assert - side effects are fire-and-forget, so command returns success even if user not found for email
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [BitAutoData]
    public async Task AutomaticallyConfirmOrganizationUserAsync_WhenDeleteDeviceRegistrationFails_LogsErrorButReturnsSuccess(
        SutProvider<AutomaticallyConfirmOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        User user,
        Guid performingUserId,
        string key,
        string defaultCollectionName,
        Device device)
    {
        // Arrange
        organizationUser.UserId = user.Id;
        organizationUser.OrganizationId = organization.Id;
        device.UserId = user.Id;
        device.PushToken = "test-push-token";
        var request = new AutomaticallyConfirmOrganizationUserRequest
        {
            OrganizationUserId = organizationUser.Id,
            OrganizationId = organization.Id,
            Key = key,
            DefaultUserCollectionName = defaultCollectionName,
            PerformedBy = new StandardUser(performingUserId, true)
        };

        SetupRepositoryMocks(sutProvider, organizationUser, organization, user);
        SetupValidatorMock(sutProvider, request, organizationUser, organization, true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmOrganizationUserAsync(Arg.Is<AcceptedOrganizationUserToConfirm>(o =>
                o.OrganizationUserId == organizationUser.Id && o.Key == request.Key))
            .Returns(true);

        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns(new List<Device> { device });

        var deviceException = new Exception("Device registration deletion failed");
        sutProvider.GetDependency<IPushRegistrationService>()
            .DeleteUserRegistrationOrganizationAsync(Arg.Any<IEnumerable<string>>(), organization.Id.ToString())
            .ThrowsAsync(deviceException);

        // Act
        var result = await sutProvider.Sut.AutomaticallyConfirmOrganizationUserAsync(request);

        // Assert - side effects are fire-and-forget, so command returns success even if device registration deletion fails
        Assert.True(result.IsSuccess);

        sutProvider.GetDependency<ILogger<AutomaticallyConfirmOrganizationUserCommand>>()
            .Received(1)
            .Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("Failed to delete device registration")),
                deviceException,
                Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [BitAutoData]
    public async Task AutomaticallyConfirmOrganizationUserAsync_WhenPushSyncOrgKeysFails_LogsErrorButReturnsSuccess(
        SutProvider<AutomaticallyConfirmOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        User user,
        Guid performingUserId,
        string key,
        string defaultCollectionName)
    {
        // Arrange
        organizationUser.UserId = user.Id;
        organizationUser.OrganizationId = organization.Id;
        var request = new AutomaticallyConfirmOrganizationUserRequest
        {
            OrganizationUserId = organizationUser.Id,
            OrganizationId = organization.Id,
            Key = key,
            DefaultUserCollectionName = defaultCollectionName,
            PerformedBy = new StandardUser(performingUserId, true)
        };

        SetupRepositoryMocks(sutProvider, organizationUser, organization, user);
        SetupValidatorMock(sutProvider, request, organizationUser, organization, true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmOrganizationUserAsync(Arg.Is<AcceptedOrganizationUserToConfirm>(o =>
                o.OrganizationUserId == organizationUser.Id && o.Key == request.Key))
            .Returns(true);

        var pushException = new Exception("Push sync failed");
        sutProvider.GetDependency<IPushNotificationService>()
            .PushSyncOrgKeysAsync(user.Id)
            .ThrowsAsync(pushException);

        // Act
        var result = await sutProvider.Sut.AutomaticallyConfirmOrganizationUserAsync(request);

        // Assert - side effects are fire-and-forget, so command returns success even if push sync fails
        Assert.True(result.IsSuccess);

        sutProvider.GetDependency<ILogger<AutomaticallyConfirmOrganizationUserCommand>>()
            .Received(1)
            .Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("Failed to push organization keys")),
                pushException,
                Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [BitAutoData]
    public async Task AutomaticallyConfirmOrganizationUserAsync_WithDevicesWithoutPushToken_FiltersCorrectly(
        SutProvider<AutomaticallyConfirmOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        User user,
        Guid performingUserId,
        string key,
        string defaultCollectionName,
        Device deviceWithToken,
        Device deviceWithoutToken)
    {
        // Arrange
        organizationUser.UserId = user.Id;
        organizationUser.OrganizationId = organization.Id;
        deviceWithToken.UserId = user.Id;
        deviceWithToken.PushToken = "test-token";
        deviceWithoutToken.UserId = user.Id;
        deviceWithoutToken.PushToken = null;
        var request = new AutomaticallyConfirmOrganizationUserRequest
        {
            OrganizationUserId = organizationUser.Id,
            OrganizationId = organization.Id,
            Key = key,
            DefaultUserCollectionName = defaultCollectionName,
            PerformedBy = new StandardUser(performingUserId, true)
        };

        SetupRepositoryMocks(sutProvider, organizationUser, organization, user);
        SetupValidatorMock(sutProvider, request, organizationUser, organization, true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmOrganizationUserAsync(Arg.Is<AcceptedOrganizationUserToConfirm>(o =>
                o.OrganizationUserId == organizationUser.Id && o.Key == request.Key))
            .Returns(true);

        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns(new List<Device> { deviceWithToken, deviceWithoutToken });

        // Act
        var result = await sutProvider.Sut.AutomaticallyConfirmOrganizationUserAsync(request);

        // Assert
        Assert.True(result.IsSuccess);

        await sutProvider.GetDependency<IPushRegistrationService>()
            .Received(1)
            .DeleteUserRegistrationOrganizationAsync(
                Arg.Is<IEnumerable<string>>(devices =>
                    devices.Count(d => deviceWithToken.Id.ToString() == d) == 1),
                organization.Id.ToString());
    }

    private static void SetupRepositoryMocks(
        SutProvider<AutomaticallyConfirmOrganizationUserCommand> sutProvider,
        OrganizationUser organizationUser,
        Organization organization,
        User user)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(user.Id)
            .Returns(user);

        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns(new List<Device>());
    }

    private static void SetupValidatorMock(
        SutProvider<AutomaticallyConfirmOrganizationUserCommand> sutProvider,
        AutomaticallyConfirmOrganizationUserRequest originalRequest,
        OrganizationUser organizationUser,
        Organization organization,
        bool isValid,
        Error? error = null)
    {
        var validationRequest = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            PerformedBy = originalRequest.PerformedBy,
            DefaultUserCollectionName = originalRequest.DefaultUserCollectionName,
            OrganizationUserId = originalRequest.OrganizationUserId,
            OrganizationUser = organizationUser,
            OrganizationId = originalRequest.OrganizationId,
            Organization = organization,
            Key = originalRequest.Key
        };

        var validationResult = isValid
            ? ValidationResultHelpers.Valid(validationRequest)
            : ValidationResultHelpers.Invalid(validationRequest, error ?? new UserIsNotAccepted());

        sutProvider.GetDependency<IAutomaticallyConfirmOrganizationUsersValidator>()
            .ValidateAsync(Arg.Any<AutomaticallyConfirmOrganizationUserValidationRequest>())
            .Returns(validationResult);
    }

    private static void SetupPolicyRequirementMock(
        SutProvider<AutomaticallyConfirmOrganizationUserCommand> sutProvider,
        Guid userId,
        Guid organizationId,
        bool requiresDefaultCollection)
    {
        var policyDetails = requiresDefaultCollection
            ? new List<PolicyDetails> { new() { OrganizationId = organizationId } }
            : new List<PolicyDetails>();

        var policyRequirement = new OrganizationDataOwnershipPolicyRequirement(
            requiresDefaultCollection ? OrganizationDataOwnershipState.Enabled : OrganizationDataOwnershipState.Disabled,
            policyDetails);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(userId)
            .Returns(policyRequirement);
    }

    private static async Task AssertSuccessfulOperationsAsync(
        SutProvider<AutomaticallyConfirmOrganizationUserCommand> sutProvider,
        OrganizationUser organizationUser,
        Organization organization,
        User user,
        string key)
    {
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(
                Arg.Is<OrganizationUser>(x => x.Id == organizationUser.Id),
                EventType.OrganizationUser_AutomaticallyConfirmed,
                Arg.Any<DateTime?>());

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendOrganizationConfirmedEmailAsync(
                organization.Name,
                user.Email,
                organizationUser.AccessSecretsManager);

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncOrgKeysAsync(user.Id);

        await sutProvider.GetDependency<IPushRegistrationService>()
            .Received(1)
            .DeleteUserRegistrationOrganizationAsync(
                Arg.Any<IEnumerable<string>>(),
                organization.Id.ToString());
    }
}
