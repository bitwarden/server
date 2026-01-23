// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Services;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Microsoft.AspNetCore.DataProtection;
using OneOf.Types;
using Error = Bit.Core.AdminConsole.Utilities.v2.Error;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class InitPendingOrganizationCommand : IInitPendingOrganizationCommand
{

    private readonly IOrganizationService _organizationService;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory;
    private readonly IDataProtector _dataProtector;
    private readonly IGlobalSettings _globalSettings;
    private readonly IPolicyService _policyService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IFeatureService _featureService;
    private readonly IPolicyRequirementQuery _policyRequirementQuery;
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery;
    private readonly IEventService _eventService;
    private readonly IMailService _mailService;
    private readonly IUserRepository _userRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IPushRegistrationService _pushRegistrationService;
    private readonly IDeviceRepository _deviceRepository;

    public InitPendingOrganizationCommand(
            IOrganizationService organizationService,
            ICollectionRepository collectionRepository,
            IOrganizationRepository organizationRepository,
            IDataProtectorTokenFactory<OrgUserInviteTokenable> orgUserInviteTokenDataFactory,
            IDataProtectionProvider dataProtectionProvider,
            IGlobalSettings globalSettings,
            IPolicyService policyService,
            IOrganizationUserRepository organizationUserRepository,
            IFeatureService featureService,
            IPolicyRequirementQuery policyRequirementQuery,
            ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
            IEventService eventService,
            IMailService mailService,
            IUserRepository userRepository,
            IPushNotificationService pushNotificationService,
            IPushRegistrationService pushRegistrationService,
            IDeviceRepository deviceRepository
            )
    {
        _organizationService = organizationService;
        _collectionRepository = collectionRepository;
        _organizationRepository = organizationRepository;
        _orgUserInviteTokenDataFactory = orgUserInviteTokenDataFactory;
        _dataProtector = dataProtectionProvider.CreateProtector(OrgUserInviteTokenable.DataProtectorPurpose);
        _globalSettings = globalSettings;
        _policyService = policyService;
        _organizationUserRepository = organizationUserRepository;
        _featureService = featureService;
        _policyRequirementQuery = policyRequirementQuery;
        _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
        _eventService = eventService;
        _mailService = mailService;
        _userRepository = userRepository;
        _pushNotificationService = pushNotificationService;
        _pushRegistrationService = pushRegistrationService;
        _deviceRepository = deviceRepository;
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
        if (orgUser == null)
        {
            return new OrganizationUserNotFoundError();
        }

        if (!ValidateInviteToken(orgUser, request.User, request.EmailToken))
        {
            return new InvalidTokenError();
        }

        var validationError = ValidateUserEmail(orgUser, request.User);
        if (validationError != null)
        {
            return validationError;
        }

        var org = await _organizationRepository.GetByIdAsync(request.OrganizationId);
        if (org == null)
        {
            return new OrganizationNotFoundError();
        }

        if (orgUser.OrganizationId != request.OrganizationId)
        {
            return new OrganizationMismatchError();
        }

        validationError = ValidateOrganizationState(org);
        if (validationError != null)
        {
            return validationError;
        }

        validationError = await ValidatePoliciesAsync(request.User, request.OrganizationId, org, orgUser);
        if (validationError != null)
        {
            return validationError;
        }

        await _organizationRepository.InitializePendingOrganizationAsync(
            request.OrganizationId,
            request.PublicKey,
            request.PrivateKey,
            request.OrganizationUserId,
            request.User.Id,
            request.UserKey,
            request.CollectionName);

        await SendNotificationsAsync(org, orgUser, request.User, request.OrganizationId);

        return new None();
    }

    private static Error ValidateUserEmail(OrganizationUser orgUser, User user)
    {
        if (string.IsNullOrWhiteSpace(orgUser.Email) ||
            !orgUser.Email.Equals(user.Email, StringComparison.InvariantCultureIgnoreCase))
        {
            return new EmailMismatchError();
        }

        return null;
    }

    private static Error ValidateOrganizationState(Organization org)
    {
        if (org.Enabled)
        {
            return new OrganizationAlreadyEnabledError();
        }

        if (org.Status != OrganizationStatusType.Pending)
        {
            return new OrganizationNotPendingError();
        }

        if (!string.IsNullOrEmpty(org.PublicKey) || !string.IsNullOrEmpty(org.PrivateKey))
        {
            return new OrganizationHasKeysError();
        }

        return null;
    }

    private async Task<Error> ValidatePoliciesAsync(User user, Guid organizationId, Organization org, OrganizationUser orgUser)
    {
        // Enforce Automatic User Confirmation Policy (when feature flag is enabled)
        if (_featureService.IsEnabled(FeatureFlagKeys.AutomaticConfirmUsers))
        {
            var autoConfirmReq = await _policyRequirementQuery.GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id);
            if (autoConfirmReq.CannotCreateNewOrganization())
            {
                return new SingleOrgPolicyViolationError();
            }
        }

        // Enforce Single Organization Policy
        var anySingleOrgPolicies = await _policyService.AnyPoliciesApplicableToUserAsync(user.Id, PolicyType.SingleOrg);
        if (anySingleOrgPolicies)
        {
            return new SingleOrgPolicyViolationError();
        }

        var twoFactorReq = await _policyRequirementQuery.GetAsync<RequireTwoFactorPolicyRequirement>(user.Id);
        if (twoFactorReq.IsTwoFactorRequiredForOrganization(organizationId) &&
            !await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(user))
        {
            return new TwoFactorRequiredError();
        }

        if (org.PlanType == PlanType.Free &&
            (orgUser.Type == OrganizationUserType.Owner || orgUser.Type == OrganizationUserType.Admin))
        {
            var adminCount = await _organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(user.Id);
            if (adminCount > 0)
            {
                return new FreeOrgAdminLimitError();
            }
        }

        return null;
    }

    private async Task SendNotificationsAsync(Organization org, OrganizationUser orgUser, User user, Guid organizationId)
    {
        await _eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Confirmed);
        await _mailService.SendOrganizationConfirmedEmailAsync(org.DisplayName(), user.Email, orgUser.AccessSecretsManager);
        await _pushNotificationService.PushSyncOrgKeysAsync(user.Id);

        var devices = await _deviceRepository.GetManyByUserIdAsync(user.Id);
        var deviceIds = devices
            .Where(d => !string.IsNullOrWhiteSpace(d.PushToken))
            .Select(d => d.Id.ToString());
        await _pushRegistrationService.DeleteUserRegistrationOrganizationAsync(deviceIds, organizationId.ToString());
    }
}
