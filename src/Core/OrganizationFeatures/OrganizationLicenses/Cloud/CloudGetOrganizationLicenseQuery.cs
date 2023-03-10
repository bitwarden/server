using Bit.Core.Entities;
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

    public CloudGetOrganizationLicenseQuery(
        IInstallationRepository installationRepository,
        IPaymentService paymentService,
        ILicensingService licensingService)
    {
        _installationRepository = installationRepository;
        _paymentService = paymentService;
        _licensingService = licensingService;
    }

    public async Task<OrganizationLicense> GetLicenseAsync(Organization organization, Guid installationId,
        int? version = null)
    {
        var installation = await _installationRepository.GetByIdAsync(installationId);
        if (installation is not { Enabled: true })
        {
            throw new BadRequestException("Invalid installation id");
        }

        var subscriptionInfo = await _paymentService.GetSubscriptionAsync(organization);
        return new OrganizationLicense(organization, subscriptionInfo, installationId, _licensingService, version);
    }
}
