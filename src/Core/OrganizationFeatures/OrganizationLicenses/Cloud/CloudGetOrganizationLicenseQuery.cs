using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;
using Bit.Core.Repositories;
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
        return new OrganizationLicense(organization, subscriptionInfo, installationId, _licensingService, version);
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
