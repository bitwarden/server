using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Billing;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Services;
using Bit.Core.Vault.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public class OrganizationDeleteCommand : IOrganizationDeleteCommand
{
    private readonly IOrganizationAbilityCacheService _organizationAbilityCacheService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly ICipherService _cipherService;
    private readonly ISubscriberService _subscriberService;
    private readonly ISendFileStorageService _sendFileStorageService;
    private readonly ILogger<OrganizationDeleteCommand> _logger;

    public OrganizationDeleteCommand(
        IOrganizationAbilityCacheService organizationAbilityCacheService,
        IOrganizationRepository organizationRepository,
        ISsoConfigRepository ssoConfigRepository,
        ICipherService cipherService,
        ISubscriberService subscriberService,
        ISendFileStorageService sendFileStorageService,
        ILogger<OrganizationDeleteCommand> logger)
    {
        _organizationAbilityCacheService = organizationAbilityCacheService;
        _organizationRepository = organizationRepository;
        _ssoConfigRepository = ssoConfigRepository;
        _cipherService = cipherService;
        _subscriberService = subscriberService;
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

                // In cases where the subscription is not active, the cancellation will fail and be logged.
                await _subscriberService.CancelSubscription(organization, cancelImmediately: !eop);
            }
            catch (Exception exception) when (exception is GatewayException or BillingException)
            {
                _logger.LogWarning(exception, "Failed to cancel subscription for organization {OrganizationId}", organization.Id);
            }
        }

        await _sendFileStorageService.DeleteFilesForOrganizationAsync(organization.Id);
        await _cipherService.DeleteAttachmentsForOrganizationAsync(organization.Id);
        await _organizationRepository.DeleteAsync(organization);
        await _organizationAbilityCacheService.DeleteOrganizationAbilityAsync(organization.Id);
    }

    private async Task ValidateDeleteOrganizationAsync(Organization organization)
    {
        var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
        if (ssoConfig?.GetData()?.MemberDecryptionType == MemberDecryptionType.KeyConnector)
        {
            throw new BadRequestException("You cannot delete an Organization that is using Key Connector.");
        }
    }
}
