using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Billing;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public class OrganizationDeleteCommand : IOrganizationDeleteCommand
{
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IStripePaymentService _paymentService;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly ISubscriberService _subscriberService;
    private readonly IFeatureService _featureService;
    private readonly ISendRepository _sendRepository;
    private readonly ISendFileStorageService _sendFileStorageService;
    private readonly ILogger<OrganizationDeleteCommand> _logger;

    public OrganizationDeleteCommand(
        IApplicationCacheService applicationCacheService,
        IOrganizationRepository organizationRepository,
        IStripePaymentService paymentService,
        ISsoConfigRepository ssoConfigRepository,
        ISubscriberService subscriberService,
        IFeatureService featureService,
        ISendRepository sendRepository,
        ISendFileStorageService sendFileStorageService,
        ILogger<OrganizationDeleteCommand> logger)
    {
        _applicationCacheService = applicationCacheService;
        _organizationRepository = organizationRepository;
        _paymentService = paymentService;
        _ssoConfigRepository = ssoConfigRepository;
        _subscriberService = subscriberService;
        _featureService = featureService;
        _sendRepository = sendRepository;
        _sendFileStorageService = sendFileStorageService;
        _logger = logger;
    }

    public async Task DeleteAsync(Organization organization)
    {
        await ValidateDeleteOrganizationAsync(organization);

        if (!string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            try
            {
                var eop = !organization.ExpirationDate.HasValue ||
                          organization.ExpirationDate.Value >= DateTime.UtcNow;

                if (_featureService.IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal))
                {
                    // In cases where the subscription is not active, the cancellation will fail and be logged.
                    await _subscriberService.CancelSubscription(organization, cancelImmediately: !eop);
                }
                else
                {
                    await _paymentService.CancelSubscriptionAsync(organization, eop);
                }
            }
            catch (Exception exception) when (exception is GatewayException or BillingException)
            {
                _logger.LogWarning(exception, "Failed to cancel subscription for organization {OrganizationId}", organization.Id);
            }
        }


        await DeleteOrganizationOwnedSendsAsync(organization);
        await _organizationRepository.DeleteAsync(organization);
        await _applicationCacheService.DeleteOrganizationAbilityAsync(organization.Id);
    }

    private async Task ValidateDeleteOrganizationAsync(Organization organization)
    {
        var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
        if (ssoConfig?.GetData()?.MemberDecryptionType == MemberDecryptionType.KeyConnector)
        {
            throw new BadRequestException("You cannot delete an Organization that is using Key Connector.");
        }
    }

    private async Task DeleteOrganizationOwnedSendsAsync(Organization organization)
    {
        var sends = await _sendRepository.GetManyByOrganizationIdAsync(organization.Id);
        foreach (var send in sends.Where(s => s.Type == SendType.File))
        {
            var data = send.Data != null ? JsonSerializer.Deserialize<SendFileData>(send.Data) : null;
            if (data?.Id != null)
            {
                await _sendFileStorageService.DeleteFileAsync(send, data.Id);
            }
        }
    }
}
