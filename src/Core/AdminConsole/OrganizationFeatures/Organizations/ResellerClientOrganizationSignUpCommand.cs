using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Billing.Pricing;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.StaticStore;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public class ResellerClientOrganizationSignUpCommand : IResellerClientOrganizationSignUpCommand
{
    private readonly ICurrentContext _currentContext;
    private readonly IPricingClient _pricingClient;
    private readonly IReferenceEventService _referenceEventService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPushRegistrationService _pushRegistrationService;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IPaymentService _paymentService;

    public ResellerClientOrganizationSignUpCommand(
        ICurrentContext currentContext,
        IPricingClient pricingClient,
        IReferenceEventService referenceEventService,
        IOrganizationRepository organizationRepository,
        IOrganizationApiKeyRepository organizationApiKeyRepository,
        IApplicationCacheService applicationCacheService,
        IOrganizationUserRepository organizationUserRepository,
        IPushRegistrationService pushRegistrationService,
        IPushNotificationService pushNotificationService,
        ICollectionRepository collectionRepository,
        IDeviceRepository deviceRepository,
        IPaymentService paymentService)
    {
        _currentContext = currentContext;
        _pricingClient = pricingClient;
        _referenceEventService = referenceEventService;
        _organizationRepository = organizationRepository;
        _organizationApiKeyRepository = organizationApiKeyRepository;
        _applicationCacheService = applicationCacheService;
        _organizationUserRepository = organizationUserRepository;
        _pushRegistrationService = pushRegistrationService;
        _pushNotificationService = pushNotificationService;
        _collectionRepository = collectionRepository;
        _deviceRepository = deviceRepository;
        _paymentService = paymentService;
    }

    public async Task<(Organization organization, OrganizationUser organizationUser, Collection defaultCollection)> SignupClientAsync(OrganizationSignup signup)
    {
        var plan = await _pricingClient.GetPlanOrThrow(signup.Plan);

        ValidatePlan(plan, signup.AdditionalSeats, "Password Manager");

        var organization = new Organization
        {
            // Pre-generate the org id so that we can save it with the Stripe subscription.
            Id = CoreHelpers.GenerateComb(),
            Name = signup.Name,
            BillingEmail = signup.BillingEmail,
            PlanType = plan!.Type,
            Seats = signup.AdditionalSeats,
            MaxCollections = plan.PasswordManager.MaxCollections,
            MaxStorageGb = 1,
            UsePolicies = plan.HasPolicies,
            UseSso = plan.HasSso,
            UseGroups = plan.HasGroups,
            UseEvents = plan.HasEvents,
            UseDirectory = plan.HasDirectory,
            UseTotp = plan.HasTotp,
            Use2fa = plan.Has2fa,
            UseApi = plan.HasApi,
            UseResetPassword = plan.HasResetPassword,
            SelfHost = plan.HasSelfHost,
            UsersGetPremium = plan.UsersGetPremium,
            UseCustomPermissions = plan.HasCustomPermissions,
            UseScim = plan.HasScim,
            Plan = plan.Name,
            Gateway = GatewayType.Stripe,
            ReferenceData = signup.Owner.ReferenceData,
            Enabled = true,
            LicenseKey = CoreHelpers.SecureRandomString(20),
            PublicKey = signup.PublicKey,
            PrivateKey = signup.PrivateKey,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
            Status = OrganizationStatusType.Created,
            UsePasswordManager = true,
            // Secrets Manager not available for purchase with Consolidated Billing.
            UseSecretsManager = false,
        };

        var returnValue = await SignUpAsync(organization, default, signup.OwnerKey, signup.CollectionName, false);

        await _referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.Signup, organization, _currentContext)
            {
                PlanName = plan.Name,
                PlanType = plan.Type,
                Seats = returnValue.Item1.Seats,
                SignupInitiationPath = signup.InitiationPath,
                Storage = returnValue.Item1.MaxStorageGb,
            });

        return returnValue;
    }

    private static void ValidatePlan(Plan plan, int additionalSeats, string productType)
    {
        if (plan is null)
        {
            throw new BadRequestException($"{productType} Plan was null.");
        }

        if (plan.Disabled)
        {
            throw new BadRequestException($"{productType} Plan not found.");
        }

        if (additionalSeats < 0)
        {
            throw new BadRequestException($"You can't subtract {productType} seats!");
        }
    }

    /// <summary>
    /// Private helper method to create a new organization.
    /// This is common code used by both the cloud and self-hosted methods.
    /// </summary>
    private async Task<(Organization organization, OrganizationUser organizationUser, Collection defaultCollection)> SignUpAsync(Organization organization,
        Guid ownerId, string ownerKey, string collectionName, bool withPayment)
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
            OrganizationUser orgUser = null;
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

                var devices = await GetUserDeviceIdsAsync(orgUser.UserId.Value);
                await _pushRegistrationService.AddUserRegistrationOrganizationAsync(devices,
                    organization.Id.ToString());
                await _pushNotificationService.PushSyncOrgKeysAsync(ownerId);
            }

            Collection defaultCollection = null;
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
                List<CollectionAccessSelection> defaultOwnerAccess = null;
                if (orgUser != null)
                {
                    defaultOwnerAccess =
                        [new CollectionAccessSelection { Id = orgUser.Id, HidePasswords = false, ReadOnly = false, Manage = true }];
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
