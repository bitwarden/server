using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationConfirmation;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class InitPendingOrganizationCommand : IInitPendingOrganizationCommand
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IEventService _eventService;
    private readonly IUserRepository _userRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IPushRegistrationService _pushRegistrationService;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IInitPendingOrganizationValidator _validator;
    private readonly TimeProvider _timeProvider;
    private readonly ISendOrganizationConfirmationCommand _sendOrganizationConfirmationCommand;

    public InitPendingOrganizationCommand(
            ICollectionRepository collectionRepository,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IEventService eventService,
            IUserRepository userRepository,
            IPushNotificationService pushNotificationService,
            IPushRegistrationService pushRegistrationService,
            IDeviceRepository deviceRepository,
            IInitPendingOrganizationValidator validator,
            TimeProvider timeProvider,
            ISendOrganizationConfirmationCommand sendOrganizationConfirmationCommand)
    {
        _collectionRepository = collectionRepository;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _eventService = eventService;
        _userRepository = userRepository;
        _pushNotificationService = pushNotificationService;
        _pushRegistrationService = pushRegistrationService;
        _deviceRepository = deviceRepository;
        _validator = validator;
        _timeProvider = timeProvider;
        _sendOrganizationConfirmationCommand = sendOrganizationConfirmationCommand;
    }

    public async Task<CommandResult> InitPendingOrganizationAsync(InitPendingOrganizationRequest request)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(request.OrganizationUserId);
        if (orgUser is null)
        {
            return new OrganizationUserNotFoundError();
        }

        var org = await _organizationRepository.GetByIdAsync(request.OrganizationId);
        if (org is null)
        {
            return new OrganizationNotFoundError();
        }

        var validationRequest = new InitPendingOrganizationValidationRequest
        {
            User = request.User,
            OrganizationId = request.OrganizationId,
            OrganizationUserId = request.OrganizationUserId,
            OrganizationKeys = request.OrganizationKeys,
            CollectionName = request.CollectionName,
            EmailToken = request.EmailToken,
            EncryptedOrganizationSymmetricKey = request.EncryptedOrganizationSymmetricKey,
            Organization = org,
            OrganizationUser = orgUser,
        };

        var validationResult = await _validator.ValidateAsync(validationRequest);
        if (validationResult.IsError)
        {
            return validationResult.AsError;
        }

        PrepareOrganizationForInitialization(org, request);
        PrepareOrganizationUserForConfirmation(orgUser, request);

        var confirmOwnerAction = _organizationUserRepository.BuildConfirmOwnerAction(orgUser);
        await _organizationRepository.InitializeOrganizationAsync(org, confirmOwnerAction);

        await VerifyUserEmailAsync(request.User);
        await CreateDefaultCollectionAsync(orgUser, request);

        await SendNotificationsAsync(org, orgUser, request.User);

        return new None();
    }

    private void PrepareOrganizationForInitialization(Organization org, InitPendingOrganizationRequest request)
    {
        org.Enabled = true;
        org.Status = OrganizationStatusType.Created;
        org.PublicKey = request.OrganizationKeys.PublicKey;
        org.PrivateKey = request.OrganizationKeys.WrappedPrivateKey;
        org.RevisionDate = _timeProvider.GetUtcNow().UtcDateTime;
    }

    private static void PrepareOrganizationUserForConfirmation(OrganizationUser orgUser, InitPendingOrganizationRequest request)
    {
        orgUser.Status = OrganizationUserStatusType.Confirmed;
        orgUser.UserId = request.User.Id;
        orgUser.Key = request.EncryptedOrganizationSymmetricKey;
        orgUser.Email = null;
    }

    private async Task VerifyUserEmailAsync(User user)
    {
        if (!user.EmailVerified)
        {
            user.EmailVerified = true;
            await _userRepository.ReplaceAsync(user);
        }
    }

    private async Task CreateDefaultCollectionAsync(OrganizationUser orgUser, InitPendingOrganizationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CollectionName))
        {
            return;
        }

        List<CollectionAccessSelection> defaultOwnerAccess =
        [
            new() { Id = orgUser.Id, HidePasswords = false, ReadOnly = false, Manage = true }
        ];

        var defaultCollection = new Collection
        {
            Name = request.CollectionName,
            OrganizationId = request.OrganizationId
        };

        await _collectionRepository.CreateAsync(
            obj: defaultCollection,
            groups: null,
            users: defaultOwnerAccess);
    }

    private async Task SendNotificationsAsync(Organization org, OrganizationUser orgUser, User user)
    {
        await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Confirmed);

        await _sendOrganizationConfirmationCommand.SendConfirmationAsync(org, user.Email, orgUser.AccessSecretsManager);

        await _pushNotificationService.PushSyncOrgKeysAsync(user.Id);

        var devices = await _deviceRepository.GetManyByUserIdAsync(user.Id);
        var deviceIds = devices
            .Where(d => !string.IsNullOrWhiteSpace(d.PushToken))
            .Select(d => d.Id.ToString());
        await _pushRegistrationService.DeleteUserRegistrationOrganizationAsync(deviceIds, org.Id.ToString());
    }
}
