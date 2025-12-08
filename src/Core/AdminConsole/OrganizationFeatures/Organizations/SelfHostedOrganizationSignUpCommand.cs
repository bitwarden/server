using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public class SelfHostedOrganizationSignUpCommand : ISelfHostedOrganizationSignUpCommand
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IPushRegistrationService _pushRegistrationService;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IDeviceRepository _deviceRepository;
    private readonly ILicensingService _licensingService;
    private readonly IPolicyService _policyService;
    private readonly IGlobalSettings _globalSettings;
    private readonly IPaymentService _paymentService;
    private readonly IFeatureService _featureService;
    private readonly IPolicyRequirementQuery _policyRequirementQuery;

    public SelfHostedOrganizationSignUpCommand(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationApiKeyRepository organizationApiKeyRepository,
        IApplicationCacheService applicationCacheService,
        ICollectionRepository collectionRepository,
        IPushRegistrationService pushRegistrationService,
        IPushNotificationService pushNotificationService,
        IDeviceRepository deviceRepository,
        ILicensingService licensingService,
        IPolicyService policyService,
        IGlobalSettings globalSettings,
        IPaymentService paymentService,
        IFeatureService featureService,
        IPolicyRequirementQuery policyRequirementQuery)
    {
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _organizationApiKeyRepository = organizationApiKeyRepository;
        _applicationCacheService = applicationCacheService;
        _collectionRepository = collectionRepository;
        _pushRegistrationService = pushRegistrationService;
        _pushNotificationService = pushNotificationService;
        _deviceRepository = deviceRepository;
        _licensingService = licensingService;
        _policyService = policyService;
        _globalSettings = globalSettings;
        _paymentService = paymentService;
        _featureService = featureService;
        _policyRequirementQuery = policyRequirementQuery;
    }

    public async Task<(Organization organization, OrganizationUser? organizationUser)> SignUpAsync(
        OrganizationLicense license, User owner, string ownerKey, string? collectionName, string publicKey,
        string privateKey)
    {
        if (license.LicenseType != LicenseType.Organization)
        {
            throw new BadRequestException("Premium licenses cannot be applied to an organization. " +
                                          "Upload this license from your personal account settings page.");
        }

        var claimsPrincipal = _licensingService.GetClaimsPrincipalFromLicense(license);
        var canUse = license.CanUse(_globalSettings, _licensingService, claimsPrincipal, out var exception);

        if (!canUse)
        {
            throw new BadRequestException(exception);
        }

        var enabledOrgs = await _organizationRepository.GetManyByEnabledAsync();
        if (enabledOrgs.Any(o => string.Equals(o.LicenseKey, license.LicenseKey)))
        {
            throw new BadRequestException("License is already in use by another organization.");
        }

        await ValidateSignUpPoliciesAsync(owner.Id);

        var organization = claimsPrincipal != null
            // If the ClaimsPrincipal exists (there's a token on the license), use it to build the organization.
            ? OrganizationFactory.Create(owner, claimsPrincipal, publicKey, privateKey)
            // If there's no ClaimsPrincipal (there's no token on the license), use the license to build the organization.
            : OrganizationFactory.Create(owner, license, publicKey, privateKey);

        var result = await SignUpAsync(organization, owner.Id, ownerKey, collectionName, false);

        var dir = $"{_globalSettings.LicenseDirectory}/organization";
        Directory.CreateDirectory(dir);
        await using var fs = new FileStream(Path.Combine(dir, $"{organization.Id}.json"), FileMode.Create);
        await JsonSerializer.SerializeAsync(fs, license, JsonHelpers.Indented);
        return (result.organization, result.organizationUser);
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

    /// <summary>
    /// Private helper method to create a new organization.
    /// This is common code used by both the cloud and self-hosted methods.
    /// </summary>
    private async Task<(Organization organization, OrganizationUser? organizationUser, Collection? defaultCollection)>
        SignUpAsync(Organization organization,
            Guid ownerId, string ownerKey, string? collectionName, bool withPayment)
    {
        try
        {
            await _organizationRepository.CreateAsync(organization);
            await _organizationApiKeyRepository.CreateAsync(new OrganizationApiKey
            {
                OrganizationId = organization.Id,
                ApiKey = CoreHelpers.SecureRandomString(30),
                Type = OrganizationApiKeyType.Default,
                RevisionDate = DateTime.UtcNow,
            });
            await _applicationCacheService.UpsertOrganizationAbilityAsync(organization);

            // ownerId == default if the org is created by a provider - in this case it's created without an
            // owner and the first owner is immediately invited afterwards
            OrganizationUser? orgUser = null;
            if (ownerId != default)
            {
                orgUser = new OrganizationUser
                {
                    OrganizationId = organization.Id,
                    UserId = ownerId,
                    Key = ownerKey,
                    AccessSecretsManager = organization.UseSecretsManager,
                    Type = OrganizationUserType.Owner,
                    Status = OrganizationUserStatusType.Confirmed,
                    CreationDate = organization.CreationDate,
                    RevisionDate = organization.CreationDate
                };
                orgUser.SetNewId();

                await _organizationUserRepository.CreateAsync(orgUser);

                var devices = await GetUserDeviceIdsAsync(orgUser.UserId!.Value);
                await _pushRegistrationService.AddUserRegistrationOrganizationAsync(devices,
                    organization.Id.ToString());
                await _pushNotificationService.PushSyncOrgKeysAsync(ownerId);
            }

            Collection? defaultCollection = null;
            if (!string.IsNullOrWhiteSpace(collectionName))
            {
                defaultCollection = new Collection
                {
                    Name = collectionName,
                    OrganizationId = organization.Id,
                    CreationDate = organization.CreationDate,
                    RevisionDate = organization.CreationDate
                };

                // Give the owner Can Manage access over the default collection
                List<CollectionAccessSelection>? defaultOwnerAccess = null;
                if (orgUser != null)
                {
                    defaultOwnerAccess =
                    [
                        new CollectionAccessSelection
                        {
                            Id = orgUser.Id,
                            HidePasswords = false,
                            ReadOnly = false,
                            Manage = true
                        }
                    ];
                }

                await _collectionRepository.CreateAsync(defaultCollection, null, defaultOwnerAccess);
            }

            return (organization, orgUser, defaultCollection);
        }
        catch
        {
            if (withPayment)
            {
                await _paymentService.CancelAndRecoverChargesAsync(organization);
            }

            if (organization.Id != default(Guid))
            {
                await _organizationRepository.DeleteAsync(organization);
                await _applicationCacheService.DeleteOrganizationAbilityAsync(organization.Id);
            }

            throw;
        }
    }

    private async Task<IEnumerable<string>> GetUserDeviceIdsAsync(Guid userId)
    {
        var devices = await _deviceRepository.GetManyByUserIdAsync(userId);
        return devices
            .Where(d => !string.IsNullOrWhiteSpace(d.PushToken))
            .Select(d => d.Id.ToString());
    }
}
