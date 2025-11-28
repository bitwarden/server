// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Pricing;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public record ProviderClientOrganizationSignUpResponse(
    Organization Organization,
    Collection DefaultCollection);

public interface IProviderClientOrganizationSignUpCommand
{
    /// <summary>
    /// Sign up a new client organization for a provider.
    /// </summary>
    /// <param name="signup">The signup information.</param>
    /// <returns>A tuple containing the new organization and its default collection.</returns>
    Task<ProviderClientOrganizationSignUpResponse> SignUpClientOrganizationAsync(OrganizationSignup signup);
}

public class ProviderClientOrganizationSignUpCommand : IProviderClientOrganizationSignUpCommand
{
    public const string PlanNullErrorMessage = "Password Manager Plan was null.";
    public const string PlanDisabledErrorMessage = "Password Manager Plan is disabled.";
    public const string AdditionalSeatsNegativeErrorMessage = "You can't subtract Password Manager seats!";

    private readonly ICurrentContext _currentContext;
    private readonly IPricingClient _pricingClient;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly ICollectionRepository _collectionRepository;

    public ProviderClientOrganizationSignUpCommand(
        ICurrentContext currentContext,
        IPricingClient pricingClient,
        IOrganizationRepository organizationRepository,
        IOrganizationApiKeyRepository organizationApiKeyRepository,
        IApplicationCacheService applicationCacheService,
        ICollectionRepository collectionRepository)
    {
        _currentContext = currentContext;
        _pricingClient = pricingClient;
        _organizationRepository = organizationRepository;
        _organizationApiKeyRepository = organizationApiKeyRepository;
        _applicationCacheService = applicationCacheService;
        _collectionRepository = collectionRepository;
    }

    public async Task<ProviderClientOrganizationSignUpResponse> SignUpClientOrganizationAsync(OrganizationSignup signup)
    {
        var plan = await _pricingClient.GetPlanOrThrow(signup.Plan);

        ValidatePlan(plan, signup.AdditionalSeats);

        var organization = new Organization
        {
            // Pre-generate the org id so that we can save it with the Stripe subscription.
            Id = CoreHelpers.GenerateComb(),
            Name = signup.Name,
            BillingEmail = signup.BillingEmail,
            PlanType = plan!.Type,
            Seats = signup.AdditionalSeats,
            MaxCollections = plan.PasswordManager.MaxCollections,
            MaxStorageGb = plan.PasswordManager.BaseStorageGb,
            UsePolicies = plan.HasPolicies,
            UseSso = plan.HasSso,
            UseOrganizationDomains = plan.HasOrganizationDomains,
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

        var returnValue = await SignUpAsync(organization, signup.CollectionName);

        return returnValue;
    }

    private static void ValidatePlan(Plan plan, int additionalSeats)
    {
        if (plan is null)
        {
            throw new BadRequestException(PlanNullErrorMessage);
        }

        if (plan.Disabled)
        {
            throw new BadRequestException(PlanDisabledErrorMessage);
        }

        if (additionalSeats < 0)
        {
            throw new BadRequestException(AdditionalSeatsNegativeErrorMessage);
        }
    }

    /// <summary>
    /// Private helper method to create a new organization.
    /// </summary>
    private async Task<ProviderClientOrganizationSignUpResponse> SignUpAsync(
        Organization organization, string collectionName)
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

                await _collectionRepository.CreateAsync(defaultCollection, null, null);
            }

            return new ProviderClientOrganizationSignUpResponse(organization, defaultCollection);
        }
        catch
        {
            if (organization.Id != default)
            {
                await _organizationRepository.DeleteAsync(organization);
                await _applicationCacheService.DeleteOrganizationAbilityAsync(organization.Id);
            }

            throw;
        }
    }
}
