using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public class OrganizationDeleteCommand : IOrganizationDeleteCommand
{
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IStripePaymentService _paymentService;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly ICipherService _cipherService;

    public OrganizationDeleteCommand(
        IApplicationCacheService applicationCacheService,
        IOrganizationRepository organizationRepository,
        IStripePaymentService paymentService,
        ISsoConfigRepository ssoConfigRepository,
        ICipherService cipherService)
    {
        _applicationCacheService = applicationCacheService;
        _organizationRepository = organizationRepository;
        _paymentService = paymentService;
        _ssoConfigRepository = ssoConfigRepository;
        _cipherService = cipherService;
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
                await _paymentService.CancelSubscriptionAsync(organization, eop);
            }
            catch (GatewayException) { }
        }

        await _cipherService.DeleteAttachmentsForOrganizationAsync(organization.Id);
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
}
