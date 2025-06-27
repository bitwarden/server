using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Licenses.Extensions;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;
using Bit.Core.Platform.Installations;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationLicenses;

public class CloudGetOrganizationLicenseQuery : ICloudGetOrganizationLicenseQuery
{
    private readonly IInstallationRepository _installationRepository;
    private readonly IPaymentService _paymentService;
    private readonly ILicensingService _licensingService;
    private readonly IProviderRepository _providerRepository;

    public CloudGetOrganizationLicenseQuery(
        IInstallationRepository installationRepository,
        IPaymentService paymentService,
        ILicensingService licensingService,
        IProviderRepository providerRepository)
    {
        _installationRepository = installationRepository;
        _paymentService = paymentService;
        _licensingService = licensingService;
        _providerRepository = providerRepository;
    }

    public async Task<OrganizationLicense> GetLicenseAsync(Organization organization, Guid installationId,
        int? version = null)
    {
        var installation = await _installationRepository.GetByIdAsync(installationId);
        if (installation is not { Enabled: true })
        {
            throw new BadRequestException("Invalid installation id");
        }

        var subscriptionInfo = await GetSubscriptionAsync(organization);
        var issued = DateTime.UtcNow;

        var license = new OrganizationLicense
        {
            Version = version.GetValueOrDefault(OrganizationLicense.CurrentLicenseFileVersion),
            LicenseType = LicenseType.Organization,
            LicenseKey = organization.LicenseKey,
            InstallationId = installationId,
            Id = organization.Id,
            Name = organization.Name,
            BillingEmail = organization.BillingEmail,
            BusinessName = organization.BusinessName,
            Enabled = organization.Enabled,
            Plan = organization.Plan,
            PlanType = organization.PlanType,
            Seats = organization.Seats,
            MaxCollections = organization.MaxCollections,
            UsePolicies = organization.UsePolicies,
            UseSso = organization.UseSso,
            UseKeyConnector = organization.UseKeyConnector,
            UseScim = organization.UseScim,
            UseGroups = organization.UseGroups,
            UseEvents = organization.UseEvents,
            UseDirectory = organization.UseDirectory,
            UseTotp = organization.UseTotp,
            Use2fa = organization.Use2fa,
            UseApi = organization.UseApi,
            UseResetPassword = organization.UseResetPassword,
            MaxStorageGb = organization.MaxStorageGb,
            SelfHost = organization.SelfHost,
            UsersGetPremium = organization.UsersGetPremium,
            UseCustomPermissions = organization.UseCustomPermissions,
            Issued = issued,
            UsePasswordManager = organization.UsePasswordManager,
            UseSecretsManager = organization.UseSecretsManager,
            SmSeats = organization.SmSeats,
            SmServiceAccounts = organization.SmServiceAccounts,
            UseRiskInsights = organization.UseRiskInsights,
            UseOrganizationDomains = organization.UseOrganizationDomains,

            // Deprecated. Left for backwards compatibility with old license versions.
            LimitCollectionCreationDeletion = organization.LimitCollectionCreation || organization.LimitCollectionDeletion,
            AllowAdminAccessToAllCollectionItems = organization.AllowAdminAccessToAllCollectionItems,

            Expires = organization.CalculateFreshExpirationDate(subscriptionInfo, issued),
            Refresh = organization.CalculateFreshRefreshDate(subscriptionInfo, organization.CalculateFreshExpirationDate(subscriptionInfo, issued), issued),
            ExpirationWithoutGracePeriod = organization.CalculateFreshExpirationDateWithoutGracePeriod(subscriptionInfo),
            Trial = organization.IsTrialing(subscriptionInfo),
            UseAdminSponsoredFamilies = organization.UseAdminSponsoredFamilies
        };

        // Hash is included in Signature, and so must be initialized before signing
        license.Hash = Convert.ToBase64String(license.ComputeHash());
        license.Signature = Convert.ToBase64String(_licensingService.SignLicense(license));
        license.Token = await _licensingService.CreateOrganizationTokenAsync(organization, installationId, subscriptionInfo);

        return license;
    }

    private async Task<SubscriptionInfo> GetSubscriptionAsync(Organization organization)
    {
        if (organization is not { Status: OrganizationStatusType.Managed })
        {
            return await _paymentService.GetSubscriptionAsync(organization);
        }

        var provider = await _providerRepository.GetByOrganizationIdAsync(organization.Id);
        return await _paymentService.GetSubscriptionAsync(provider);
    }
}
