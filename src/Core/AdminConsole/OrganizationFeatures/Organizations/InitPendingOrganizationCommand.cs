using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationConfirmation;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Services;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class InitPendingOrganizationCommand : IInitPendingOrganizationCommand
{
    private readonly IOrganizationService _organizationService;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory;
    private readonly IPolicyService _policyService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IFeatureService _featureService;
    private readonly IPolicyRequirementQuery _policyRequirementQuery;
    private readonly IEventService _eventService;
    private readonly IMailService _mailService;
    private readonly IUserRepository _userRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IPushRegistrationService _pushRegistrationService;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IInitPendingOrganizationValidator _validator;
    private readonly TimeProvider _timeProvider;
    private readonly ISendOrganizationConfirmationCommand _sendOrganizationConfirmationCommand;

    public InitPendingOrganizationCommand(
            IOrganizationService organizationService,
            ICollectionRepository collectionRepository,
            IOrganizationRepository organizationRepository,
            IDataProtectorTokenFactory<OrgUserInviteTokenable> orgUserInviteTokenDataFactory,
            IPolicyService policyService,
            IOrganizationUserRepository organizationUserRepository,
            IFeatureService featureService,
            IPolicyRequirementQuery policyRequirementQuery,
            IEventService eventService,
            IMailService mailService,
            IUserRepository userRepository,
            IPushNotificationService pushNotificationService,
            IPushRegistrationService pushRegistrationService,
            IDeviceRepository deviceRepository,
            IInitPendingOrganizationValidator validator,
            TimeProvider timeProvider,
            ISendOrganizationConfirmationCommand sendOrganizationConfirmationCommand)
    {
        _organizationService = organizationService;
        _collectionRepository = collectionRepository;
        _organizationRepository = organizationRepository;
        _orgUserInviteTokenDataFactory = orgUserInviteTokenDataFactory;
        _policyService = policyService;
        _organizationUserRepository = organizationUserRepository;
        _featureService = featureService;
        _policyRequirementQuery = policyRequirementQuery;
        _eventService = eventService;
        _mailService = mailService;
        _userRepository = userRepository;
        _pushNotificationService = pushNotificationService;
        _pushRegistrationService = pushRegistrationService;
        _deviceRepository = deviceRepository;
        _validator = validator;
        _timeProvider = timeProvider;
        _sendOrganizationConfirmationCommand = sendOrganizationConfirmationCommand;
    }

    public async Task InitPendingOrganizationAsync(User user, Guid organizationId, Guid organizationUserId, string publicKey, string privateKey, string collectionName, string emailToken)
    {
        await ValidateSignUpPoliciesAsync(user.Id);

        var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        if (orgUser == null)
        {
            throw new BadRequestException("User invalid.");
        }

        var tokenValid = ValidateInviteToken(orgUser, user, emailToken);

        if (!tokenValid)
        {
            throw new BadRequestException("Invalid token");
        }

        var org = await _organizationRepository.GetByIdAsync(organizationId);
        if (org == null)
        {
            throw new BadRequestException("Organization not found.");
        }

        if (org.Enabled)
        {
            throw new BadRequestException("Organization is already enabled.");
        }

        if (org.Status != OrganizationStatusType.Pending)
        {
            throw new BadRequestException("Organization is not on a Pending status.");
        }

        if (!string.IsNullOrEmpty(org.PublicKey))
        {
            throw new BadRequestException("Organization already has a Public Key.");
        }

        if (!string.IsNullOrEmpty(org.PrivateKey))
        {
            throw new BadRequestException("Organization already has a Private Key.");
        }

        org.Enabled = true;
        org.Status = OrganizationStatusType.Created;
        org.PublicKey = publicKey;
        org.PrivateKey = privateKey;

        await _organizationService.UpdateAsync(org);

        if (!string.IsNullOrWhiteSpace(collectionName))
        {
            // give the owner Can Manage access over the default collection
            List<CollectionAccessSelection> defaultOwnerAccess =
                [new CollectionAccessSelection { Id = orgUser.Id, HidePasswords = false, ReadOnly = false, Manage = true }];

            var defaultCollection = new Collection
            {
                Name = collectionName,
                OrganizationId = org.Id
            };
            await _collectionRepository.CreateAsync(defaultCollection, null, defaultOwnerAccess);
        }
    }

    private async Task ValidateSignUpPoliciesAsync(Guid ownerId)
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.AutomaticConfirmUsers))
        {
            var requirement = await _policyRequirementQuery.GetAsync<AutomaticUserConfirmationPolicyRequirement>(ownerId);

            if (requirement.CannotCreateNewOrganization())
            {
                throw new BadRequestException("You may not create an organization. You belong to an organization " +
                                              "which has a policy that prohibits you from being a member of any other organization.");
            }
        }

        var anySingleOrgPolicies = await _policyService.AnyPoliciesApplicableToUserAsync(ownerId, PolicyType.SingleOrg);
        if (anySingleOrgPolicies)
        {
            throw new BadRequestException("You may not create an organization. You belong to an organization " +
                "which has a policy that prohibits you from being a member of any other organization.");
        }
    }

    private bool ValidateInviteToken(OrganizationUser orgUser, User user, string emailToken)
    {
        var tokenValid = OrgUserInviteTokenable.ValidateOrgUserInviteStringToken(
            _orgUserInviteTokenDataFactory, emailToken, orgUser);

        return tokenValid;
    }

    public async Task<CommandResult> InitPendingOrganizationVNextAsync(InitPendingOrganizationRequest request)
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

        if (_featureService.IsEnabled(FeatureFlagKeys.OrganizationConfirmationEmail))
        {
            await _sendOrganizationConfirmationCommand.SendConfirmationAsync(org, user.Email, orgUser.AccessSecretsManager);
        }
        else
        {
            await _mailService.SendOrganizationConfirmedEmailAsync(org.DisplayName(), user.Email, orgUser.AccessSecretsManager);
        }

        await _pushNotificationService.PushSyncOrgKeysAsync(user.Id);

        var devices = await _deviceRepository.GetManyByUserIdAsync(user.Id);
        var deviceIds = devices
            .Where(d => !string.IsNullOrWhiteSpace(d.PushToken))
            .Select(d => d.Id.ToString());
        await _pushRegistrationService.DeleteUserRegistrationOrganizationAsync(deviceIds, org.Id.ToString());
    }
}
