using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.Sales;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.StaticStore;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public record SignUpOrganizationResponse(
    Organization Organization,
    OrganizationUser OrganizationUser,
    Collection DefaultCollection);

public interface ICloudOrganizationSignUpCommand
{
    Task<SignUpOrganizationResponse> SignUpOrganizationAsync(OrganizationSignup signup);
}

public class CloudOrganizationSignUpCommand(
    IOrganizationUserRepository organizationUserRepository,
    IFeatureService featureService,
    IOrganizationBillingService organizationBillingService,
    IPaymentService paymentService,
    IPolicyService policyService,
    IReferenceEventService referenceEventService,
    ICurrentContext currentContext,
    IOrganizationRepository organizationRepository,
    IOrganizationApiKeyRepository organizationApiKeyRepository,
    IApplicationCacheService applicationCacheService,
    IPushRegistrationService pushRegistrationService,
    IPushNotificationService pushNotificationService,
    ICollectionRepository collectionRepository,
    IDeviceRepository deviceRepository) : ICloudOrganizationSignUpCommand
{
    public async Task<SignUpOrganizationResponse> SignUpOrganizationAsync(OrganizationSignup signup)
    {
        var plan = StaticStore.GetPlan(signup.Plan);

        ValidatePasswordManagerPlan(plan, signup);

        if (signup.UseSecretsManager)
        {
            if (signup.IsFromProvider)
            {
                throw new BadRequestException(
                    "Organizations with a Managed Service Provider do not support Secrets Manager.");
            }
            ValidateSecretsManagerPlan(plan, signup);
        }

        if (!signup.IsFromProvider)
        {
            await ValidateSignUpPoliciesAsync(signup.Owner.Id);
        }

        var organization = new Organization
        {
            // Pre-generate the org id so that we can save it with the Stripe subscription
            Id = CoreHelpers.GenerateComb(),
            Name = signup.Name,
            BillingEmail = signup.BillingEmail,
            BusinessName = signup.BusinessName,
            PlanType = plan!.Type,
            Seats = (short)(plan.PasswordManager.BaseSeats + signup.AdditionalSeats),
            MaxCollections = plan.PasswordManager.MaxCollections,
            MaxStorageGb = !plan.PasswordManager.BaseStorageGb.HasValue ?
                (short?)null : (short)(plan.PasswordManager.BaseStorageGb.Value + signup.AdditionalStorageGb),
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
            UsersGetPremium = plan.UsersGetPremium || signup.PremiumAccessAddon,
            UseCustomPermissions = plan.HasCustomPermissions,
            UseScim = plan.HasScim,
            Plan = plan.Name,
            Gateway = null,
            ReferenceData = signup.Owner.ReferenceData,
            Enabled = true,
            LicenseKey = CoreHelpers.SecureRandomString(20),
            PublicKey = signup.PublicKey,
            PrivateKey = signup.PrivateKey,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
            Status = OrganizationStatusType.Created,
            UsePasswordManager = true,
            UseSecretsManager = signup.UseSecretsManager
        };

        if (signup.UseSecretsManager)
        {
            organization.SmSeats = plan.SecretsManager.BaseSeats + signup.AdditionalSmSeats.GetValueOrDefault();
            organization.SmServiceAccounts = plan.SecretsManager.BaseServiceAccount +
                                             signup.AdditionalServiceAccounts.GetValueOrDefault();
        }

        if (plan.Type == PlanType.Free && !signup.IsFromProvider)
        {
            var adminCount =
                await organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(signup.Owner.Id);
            if (adminCount > 0)
            {
                throw new BadRequestException("You can only be an admin of one free organization.");
            }
        }
        else if (plan.Type != PlanType.Free)
        {
            if (featureService.IsEnabled(FeatureFlagKeys.AC2476_DeprecateStripeSourcesAPI))
            {
                var sale = OrganizationSale.From(organization, signup);
                await organizationBillingService.Finalize(sale);
            }
            else
            {
                if (signup.PaymentMethodType != null)
                {
                    await paymentService.PurchaseOrganizationAsync(organization, signup.PaymentMethodType.Value,
                        signup.PaymentToken, plan, signup.AdditionalStorageGb, signup.AdditionalSeats,
                        signup.PremiumAccessAddon, signup.TaxInfo, signup.IsFromProvider, signup.AdditionalSmSeats.GetValueOrDefault(),
                        signup.AdditionalServiceAccounts.GetValueOrDefault(), signup.IsFromSecretsManagerTrial);
                }
                else
                {
                    await paymentService.PurchaseOrganizationNoPaymentMethod(organization, plan, signup.AdditionalSeats,
                        signup.PremiumAccessAddon, signup.AdditionalSmSeats.GetValueOrDefault(),
                        signup.AdditionalServiceAccounts.GetValueOrDefault(), signup.IsFromSecretsManagerTrial);
                }

            }
        }

        var ownerId = signup.IsFromProvider ? default : signup.Owner.Id;
        var returnValue = await SignUpAsync(organization, ownerId, signup.OwnerKey, signup.CollectionName, true);
        await referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.Signup, organization, currentContext)
            {
                PlanName = plan.Name,
                PlanType = plan.Type,
                Seats = returnValue.Item1.Seats,
                SignupInitiationPath = signup.InitiationPath,
                Storage = returnValue.Item1.MaxStorageGb,
                // TODO: add reference events for SmSeats and Service Accounts - see AC-1481
            });

        return new SignUpOrganizationResponse(returnValue.organization, returnValue.organizationUser, returnValue.defaultCollection);
    }

    public void ValidatePasswordManagerPlan(Plan plan, OrganizationUpgrade upgrade)
    {
        ValidatePlan(plan, upgrade.AdditionalSeats, "Password Manager");

        if (plan.PasswordManager.BaseSeats + upgrade.AdditionalSeats <= 0)
        {
            throw new BadRequestException($"You do not have any Password Manager seats!");
        }

        if (upgrade.AdditionalSeats < 0)
        {
            throw new BadRequestException($"You can't subtract Password Manager seats!");
        }

        if (!plan.PasswordManager.HasAdditionalStorageOption && upgrade.AdditionalStorageGb > 0)
        {
            throw new BadRequestException("Plan does not allow additional storage.");
        }

        if (upgrade.AdditionalStorageGb < 0)
        {
            throw new BadRequestException("You can't subtract storage!");
        }

        if (!plan.PasswordManager.HasPremiumAccessOption && upgrade.PremiumAccessAddon)
        {
            throw new BadRequestException("This plan does not allow you to buy the premium access addon.");
        }

        if (!plan.PasswordManager.HasAdditionalSeatsOption && upgrade.AdditionalSeats > 0)
        {
            throw new BadRequestException("Plan does not allow additional users.");
        }

        if (plan.PasswordManager.HasAdditionalSeatsOption && plan.PasswordManager.MaxAdditionalSeats.HasValue &&
            upgrade.AdditionalSeats > plan.PasswordManager.MaxAdditionalSeats.Value)
        {
            throw new BadRequestException($"Selected plan allows a maximum of " +
                                          $"{plan.PasswordManager.MaxAdditionalSeats.GetValueOrDefault(0)} additional users.");
        }
    }

    public void ValidateSecretsManagerPlan(Plan plan, OrganizationUpgrade upgrade)
    {
        if (plan.SupportsSecretsManager == false)
        {
            throw new BadRequestException("Invalid Secrets Manager plan selected.");
        }

        ValidatePlan(plan, upgrade.AdditionalSmSeats.GetValueOrDefault(), "Secrets Manager");

        if (plan.SecretsManager.BaseSeats + upgrade.AdditionalSmSeats <= 0)
        {
            throw new BadRequestException($"You do not have any Secrets Manager seats!");
        }

        if (!plan.SecretsManager.HasAdditionalServiceAccountOption && upgrade.AdditionalServiceAccounts > 0)
        {
            throw new BadRequestException("Plan does not allow additional Machine Accounts.");
        }

        if ((plan.ProductTier == ProductTierType.TeamsStarter &&
            upgrade.AdditionalSmSeats.GetValueOrDefault() > plan.PasswordManager.BaseSeats) ||
            (plan.ProductTier != ProductTierType.TeamsStarter &&
             upgrade.AdditionalSmSeats.GetValueOrDefault() > upgrade.AdditionalSeats))
        {
            throw new BadRequestException("You cannot have more Secrets Manager seats than Password Manager seats.");
        }

        if (upgrade.AdditionalServiceAccounts.GetValueOrDefault() < 0)
        {
            throw new BadRequestException("You can't subtract Machine Accounts!");
        }

        switch (plan.SecretsManager.HasAdditionalSeatsOption)
        {
            case false when upgrade.AdditionalSmSeats > 0:
                throw new BadRequestException("Plan does not allow additional users.");
            case true when plan.SecretsManager.MaxAdditionalSeats.HasValue &&
                           upgrade.AdditionalSmSeats > plan.SecretsManager.MaxAdditionalSeats.Value:
                throw new BadRequestException($"Selected plan allows a maximum of " +
                                              $"{plan.SecretsManager.MaxAdditionalSeats.GetValueOrDefault(0)} additional users.");
        }
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

    private async Task ValidateSignUpPoliciesAsync(Guid ownerId)
    {
        var anySingleOrgPolicies = await policyService.AnyPoliciesApplicableToUserAsync(ownerId, PolicyType.SingleOrg);
        if (anySingleOrgPolicies)
        {
            throw new BadRequestException("You may not create an organization. You belong to an organization " +
                                          "which has a policy that prohibits you from being a member of any other organization.");
        }
    }

    private async Task<(Organization organization, OrganizationUser organizationUser, Collection defaultCollection)> SignUpAsync(Organization organization,
    Guid ownerId, string ownerKey, string collectionName, bool withPayment)
    {
        try
        {
            await organizationRepository.CreateAsync(organization);
            await organizationApiKeyRepository.CreateAsync(new OrganizationApiKey
            {
                OrganizationId = organization.Id,
                ApiKey = CoreHelpers.SecureRandomString(30),
                Type = OrganizationApiKeyType.Default,
                RevisionDate = DateTime.UtcNow,
            });
            await applicationCacheService.UpsertOrganizationAbilityAsync(organization);

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

                await organizationUserRepository.CreateAsync(orgUser);

                var devices = await GetUserDeviceIdsAsync(orgUser.UserId.Value);
                await pushRegistrationService.AddUserRegistrationOrganizationAsync(devices, organization.Id.ToString());
                await pushNotificationService.PushSyncOrgKeysAsync(ownerId);
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

                await collectionRepository.CreateAsync(defaultCollection, null, defaultOwnerAccess);
            }

            return (organization, orgUser, defaultCollection);
        }
        catch
        {
            if (withPayment)
            {
                await paymentService.CancelAndRecoverChargesAsync(organization);
            }

            if (organization.Id != default(Guid))
            {
                await organizationRepository.DeleteAsync(organization);
                await applicationCacheService.DeleteOrganizationAbilityAsync(organization.Id);
            }

            throw;
        }
    }

    private async Task<IEnumerable<string>> GetUserDeviceIdsAsync(Guid userId)
    {
        var devices = await deviceRepository.GetManyByUserIdAsync(userId);
        return devices
            .Where(d => !string.IsNullOrWhiteSpace(d.PushToken))
            .Select(d => d.Id.ToString());
    }
}
