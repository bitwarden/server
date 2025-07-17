﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Platform.Installations;
using Bit.Core.Services;

namespace Bit.Core.Billing.Organizations.Queries;

public interface IGetCloudOrganizationLicenseQuery
{
    Task<OrganizationLicense> GetLicenseAsync(Organization organization, Guid installationId,
        int? version = null);
}

public class GetCloudOrganizationLicenseQuery : IGetCloudOrganizationLicenseQuery
{
    private readonly IInstallationRepository _installationRepository;
    private readonly IPaymentService _paymentService;
    private readonly ILicensingService _licensingService;
    private readonly IProviderRepository _providerRepository;
    private readonly IFeatureService _featureService;

    public GetCloudOrganizationLicenseQuery(
        IInstallationRepository installationRepository,
        IPaymentService paymentService,
        ILicensingService licensingService,
        IProviderRepository providerRepository,
        IFeatureService featureService)
    {
        _installationRepository = installationRepository;
        _paymentService = paymentService;
        _licensingService = licensingService;
        _providerRepository = providerRepository;
        _featureService = featureService;
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
