using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Providers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Commercial.Core.AdminConsole.Providers;

public class RemoveOrganizationFromProviderCommand : IRemoveOrganizationFromProviderCommand
{
    private readonly IEventService _eventService;
    private readonly IMailService _mailService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IPaymentService _paymentService;
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly IProviderRepository _providerRepository;

    public RemoveOrganizationFromProviderCommand(
        IEventService eventService,
        IMailService mailService,
        IOrganizationRepository organizationRepository,
        IOrganizationService organizationService,
        IPaymentService paymentService,
        IProviderOrganizationRepository providerOrganizationRepository,
        IProviderRepository providerRepository)
    {
        _eventService = eventService;
        _mailService = mailService;
        _organizationRepository = organizationRepository;
        _organizationService = organizationService;
        _paymentService = paymentService;
        _providerOrganizationRepository = providerOrganizationRepository;
        _providerRepository = providerRepository;
    }

    public async Task RemoveOrganizationFromProvider(Guid providerId, Guid providerOrganizationId)
    {
        var providerOrganization = await ValidateProviderOrganizationAsync(providerId, providerOrganizationId);

        var organization = await RemoveOrganizationPaymentMethodAsync(providerOrganization.OrganizationId);

        var organizationOwnerEmails = await UpdateOrganizationBillingEmailAsync(organization);

        await SendProviderUpdatePaymentMethodEmailsAsync(organization, providerId, organizationOwnerEmails);

        await DeleteProviderOrganizationAsync(providerOrganization);
    }

    private async Task<ProviderOrganization> ValidateProviderOrganizationAsync(Guid providerId, Guid providerOrganizationId)
    {
        var providerOrganization = await _providerOrganizationRepository.GetByIdAsync(providerOrganizationId);

        if (providerOrganization == null || providerOrganization.ProviderId != providerId)
        {
            throw new BadRequestException("Failed to remove organization. Please contact support.");
        }

        if (!await _organizationService.HasConfirmedOwnersExceptAsync(
                providerOrganization.OrganizationId,
                Array.Empty<Guid>(),
                includeProvider: false))
        {
            throw new BadRequestException("Organization must have at least one confirmed owner.");
        }

        return providerOrganization;
    }

    private async Task<Organization> RemoveOrganizationPaymentMethodAsync(Guid organizationId)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);

        await _paymentService.RemovePaymentMethod(organization);

        return organization;
    }

    private async Task<List<string>> UpdateOrganizationBillingEmailAsync(Organization organization)
    {
        var organizationOwnerEmails =
            (await _organizationRepository.GetOwnerEmailAddressesById(organization.Id)).ToList();

        organization.BillingEmail = organizationOwnerEmails.MinBy(email => email);

        await _organizationRepository.ReplaceAsync(organization);

        return organizationOwnerEmails;
    }

    private async Task SendProviderUpdatePaymentMethodEmailsAsync(
        Organization organization,
        Guid providerId,
        IEnumerable<string> organizationOwnerEmails)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);

        await _mailService.SendProviderUpdatePaymentMethod(
            organization.Id,
            organization.Name,
            provider.Name,
            organizationOwnerEmails);
    }

    private async Task DeleteProviderOrganizationAsync(ProviderOrganization providerOrganization)
    {
        await _providerOrganizationRepository.DeleteAsync(providerOrganization);

        await _eventService.LogProviderOrganizationEventAsync(
            providerOrganization,
            EventType.ProviderOrganization_Removed);
    }
}
