using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public class OrganizationDeleteCommand : IOrganizationDeleteCommand
{
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IAttachmentStorageService _attachmentStorageService;
    private readonly ICipherRepository _cipherRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IStripePaymentService _paymentService;
    private readonly ISsoConfigRepository _ssoConfigRepository;

    public OrganizationDeleteCommand(
        IApplicationCacheService applicationCacheService,
        IAttachmentStorageService attachmentStorageService,
        ICipherRepository cipherRepository,
        IOrganizationRepository organizationRepository,
        IStripePaymentService paymentService,
        ISsoConfigRepository ssoConfigRepository)
    {
        _applicationCacheService = applicationCacheService;
        _attachmentStorageService = attachmentStorageService;
        _cipherRepository = cipherRepository;
        _organizationRepository = organizationRepository;
        _paymentService = paymentService;
        _ssoConfigRepository = ssoConfigRepository;
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

        // Fetch cipher IDs before DB deletion so we can clean up attachment storage
        var orgCiphers = await _cipherRepository.GetManyByOrganizationIdAsync(organization.Id);

        await _organizationRepository.DeleteAsync(organization);
        await _applicationCacheService.DeleteOrganizationAbilityAsync(organization.Id);

        foreach (var cipher in orgCiphers)
        {
            await _attachmentStorageService.DeleteAttachmentsForCipherAsync(cipher.Id);
        }
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
