using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Pricing;
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
    private readonly IFeatureService _featureService;
    private readonly IPricingClient _pricingClient;

    public CloudGetOrganizationLicenseQuery(
        IInstallationRepository installationRepository,
        IPaymentService paymentService,
        ILicensingService licensingService,
        IProviderRepository providerRepository,
        IFeatureService featureService,
        IPricingClient pricingClient)
    {
        _installationRepository = installationRepository;
        _paymentService = paymentService;
        _licensingService = licensingService;
        _providerRepository = providerRepository;
        _featureService = featureService;
        _pricingClient = pricingClient;
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
        var license = new OrganizationLicense(organization, subscriptionInfo, installationId, _licensingService, version);
        var plan = await _pricingClient.GetPlan(organization.PlanType);
        int? smMaxProjects = plan?.SupportsSecretsManager ?? false
            ? plan.SecretsManager.MaxProjects
            : null;
        license.Token = await _licensingService.CreateOrganizationTokenAsync(organization, installationId, subscriptionInfo, smMaxProjects);

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
