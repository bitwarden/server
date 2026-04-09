using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Platform.Installations;

namespace Bit.Core.Billing.Organizations.Queries;

public interface IGetCloudOrganizationLicenseQuery
{
    Task<OrganizationLicense> GetLicenseAsync(Organization organization, Guid installationId,
        int? version = null);
}

public class GetCloudOrganizationLicenseQuery : IGetCloudOrganizationLicenseQuery
{
    private readonly IInstallationRepository _installationRepository;
    private readonly IStripePaymentService _paymentService;
    private readonly ILicensingService _licensingService;
    private readonly IProviderRepository _providerRepository;

    public GetCloudOrganizationLicenseQuery(
        IInstallationRepository installationRepository,
        IStripePaymentService paymentService,
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

        ValidateSubscriptionForLicenseGeneration(subscriptionInfo);

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

    private static void ValidateSubscriptionForLicenseGeneration(SubscriptionInfo subscriptionInfo)
    {
        if (subscriptionInfo?.Subscription == null)
        {
            throw new BadRequestException("No active subscription found.");
        }

        var status = subscriptionInfo.Subscription.Status;

        if (status is StripeConstants.SubscriptionStatus.Canceled or StripeConstants.SubscriptionStatus.Incomplete
            or StripeConstants.SubscriptionStatus.IncompleteExpired or StripeConstants.SubscriptionStatus.Unpaid)
        {
            throw new BadRequestException(
                "Unable to generate license due to a payment issue. Please update your billing information or contact support for assistance.");
        }
    }
}