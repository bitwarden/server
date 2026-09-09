// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.StaticStore;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public record SignUpOrganizationResponse(
    Organization Organization,
    OrganizationUser OrganizationUser);

public interface ICloudOrganizationSignUpCommand
{
    Task<SignUpOrganizationResponse> SignUpOrganizationAsync(OrganizationSignup signup);
}

public class CloudOrganizationSignUpCommand(
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationBillingService organizationBillingService,
    IStripePaymentService paymentService,
    IOrganizationRepository organizationRepository,
    IOrganizationApiKeyRepository organizationApiKeyRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService,
    IPushRegistrationService pushRegistrationService,
    IPushNotificationService pushNotificationService,
    ICollectionRepository collectionRepository,
    IDeviceRepository deviceRepository,
    IPricingClient pricingClient,
    IPolicyRequirementQuery policyRequirementQuery) : ICloudOrganizationSignUpCommand
{
    public async Task<SignUpOrganizationResponse> SignUpOrganizationAsync(OrganizationSignup signup)
    {
        var plan = await pricingClient.GetPlanOrThrow(signup.Plan);

        ValidatePasswordManagerPlan(plan, signup);
        ValidateTrialLength(signup);

        if (signup.UseSecretsManager)
        {
            if (signup.IsFromProvider)
            {
                throw new BadRequestException(
                    new SecretsManagerMspUnsupportedError().Message);
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
            MaxStorageGb = (short)(plan.PasswordManager.BaseStorageGb + signup.AdditionalStorageGb),
            UsePolicies = plan.HasPolicies,
            UseMyItems = plan.HasMyItems,
            UseInviteLinks = plan.HasInviteLinks,
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
            UseRiskInsights = plan.HasRiskInsights,
            Plan = plan.Name,
            Gateway = null,
            ReferenceData = signup.Owner.ReferenceData,
            Enabled = true,
            LicenseKey = CoreHelpers.SecureRandomString(20),
            PublicKey = signup.Keys?.PublicKey,
            PrivateKey = signup.Keys?.WrappedPrivateKey,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
            Status = OrganizationStatusType.Created,
            UsePasswordManager = true,
            UseSecretsManager = signup.UseSecretsManager,
            UseOrganizationDomains = plan.HasOrganizationDomains,
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
                throw new BadRequestException(new FreeOrgAdminLimitError().Message);
            }
        }
        else if (plan.Type != PlanType.Free)
        {
            var sale = OrganizationSale.From(organization, signup);
            await organizationBillingService.Finalize(sale);
        }

        var ownerId = signup.IsFromProvider ? default : signup.Owner.Id;
        var returnValue = await SignUpAsync(organization, ownerId, signup.OwnerKey, signup.CollectionName, true);
        return new SignUpOrganizationResponse(returnValue.organization, returnValue.organizationUser);
    }

    public void ValidatePasswordManagerPlan(Plan plan, OrganizationUpgrade upgrade)
    {
        ValidatePlan(plan, upgrade.AdditionalSeats, "Password Manager");

        if (plan.PasswordManager.BaseSeats + upgrade.AdditionalSeats <= 0)
        {
            throw new BadRequestException(new NoPasswordManagerSeatsError().Message);
        }

        if (upgrade.AdditionalSeats < 0)
        {
            throw new BadRequestException(new CannotSubtractPasswordManagerSeatsError().Message);
        }

        if (!plan.PasswordManager.HasAdditionalStorageOption && upgrade.AdditionalStorageGb > 0)
        {
            throw new BadRequestException(new PlanDoesNotAllowAdditionalStorageError().Message);
        }

        if (upgrade.AdditionalStorageGb < 0)
        {
            throw new BadRequestException(new CannotSubtractStorageError().Message);
        }

        if (!plan.PasswordManager.HasPremiumAccessOption && upgrade.PremiumAccessAddon)
        {
            throw new BadRequestException(new PlanDoesNotAllowPremiumAccessAddonError().Message);
        }

        if (!plan.PasswordManager.HasAdditionalSeatsOption && upgrade.AdditionalSeats > 0)
        {
            throw new BadRequestException(new PlanDoesNotAllowAdditionalUsersError().Message);
        }

        if (plan.PasswordManager.HasAdditionalSeatsOption && plan.PasswordManager.MaxAdditionalSeats.HasValue &&
            upgrade.AdditionalSeats > plan.PasswordManager.MaxAdditionalSeats.Value)
        {
            throw new BadRequestException(new PlanMaxAdditionalUsersExceededError(plan.PasswordManager.MaxAdditionalSeats.GetValueOrDefault(0)).Message);
        }
    }

    public void ValidateSecretsManagerPlan(Plan plan, OrganizationUpgrade upgrade)
    {
        if (plan.SupportsSecretsManager == false)
        {
            throw new BadRequestException(new InvalidSecretsManagerPlanError().Message);
        }

        ValidatePlan(plan, upgrade.AdditionalSmSeats.GetValueOrDefault(), "Secrets Manager");

        if (plan.SecretsManager.BaseSeats + upgrade.AdditionalSmSeats <= 0)
        {
            throw new BadRequestException(new NoSecretsManagerSeatsError().Message);
        }

        if (!plan.SecretsManager.HasAdditionalServiceAccountOption && upgrade.AdditionalServiceAccounts > 0)
        {
            throw new BadRequestException(new PlanDoesNotAllowAdditionalMachineAccountsError().Message);
        }

        if ((plan.ProductTier == ProductTierType.TeamsStarter &&
            upgrade.AdditionalSmSeats.GetValueOrDefault() > plan.PasswordManager.BaseSeats) ||
            (plan.ProductTier != ProductTierType.TeamsStarter &&
             upgrade.AdditionalSmSeats.GetValueOrDefault() > upgrade.AdditionalSeats))
        {
            throw new BadRequestException(new SecretsManagerSeatsMustNotExceedPasswordManagerSeatsError().Message);
        }

        if (upgrade.AdditionalServiceAccounts.GetValueOrDefault() < 0)
        {
            throw new BadRequestException(new CannotSubtractMachineAccountsError().Message);
        }

        switch (plan.SecretsManager.HasAdditionalSeatsOption)
        {
            case false when upgrade.AdditionalSmSeats > 0:
                throw new BadRequestException(new PlanDoesNotAllowAdditionalUsersError().Message);
            case true when plan.SecretsManager.MaxAdditionalSeats.HasValue &&
                           upgrade.AdditionalSmSeats > plan.SecretsManager.MaxAdditionalSeats.Value:
                throw new BadRequestException(new PlanMaxAdditionalUsersExceededError(plan.SecretsManager.MaxAdditionalSeats.GetValueOrDefault(0)).Message);
        }
    }

    private static void ValidatePlan(Plan plan, int additionalSeats, string productType)
    {
        if (plan is null)
        {
            throw new BadRequestException(new PlanNullError(productType).Message);
        }

        if (plan.Disabled)
        {
            throw new BadRequestException(new PlanNotFoundError(productType).Message);
        }

        if (additionalSeats < 0)
        {
            throw new BadRequestException(new CannotSubtractProductSeatsError(productType).Message);
        }
    }

    private async Task ValidateSignUpPoliciesAsync(Guid ownerId)
    {
        var requirement = await policyRequirementQuery.GetAsync<AutomaticUserConfirmationPolicyRequirement>(ownerId);

        if (requirement.CannotCreateNewOrganization())
        {
            throw new BadRequestException(new UserCannotCreateOrg().Message);
        }

        var singleOrgRequirement = await policyRequirementQuery.GetAsync<SingleOrganizationPolicyRequirement>(ownerId);
        var error = singleOrgRequirement.CanCreateOrganization();
        if (error is not null)
        {
            throw new BadRequestException(error.Message);
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
            await organizationAbilityCacheService.UpsertOrganizationAbilityAsync(organization);

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

            if (organization.Id != Guid.Empty)
            {
                await organizationRepository.DeleteAsync(organization);
                await organizationAbilityCacheService.DeleteOrganizationAbilityAsync(organization.Id);
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

    private static void ValidateTrialLength(OrganizationSignup signup)
    {
        if (signup.TrialLength is < 0 or > 30)
        {
            throw new BadRequestException(new TrialLengthOutOfRangeError().Message);
        }
    }
}
