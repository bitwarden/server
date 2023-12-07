using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Providers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
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
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;

    public RemoveOrganizationFromProviderCommand(
        IEventService eventService,
        IMailService mailService,
        IOrganizationRepository organizationRepository,
        IOrganizationService organizationService,
        IProviderOrganizationRepository providerOrganizationRepository)
    {
        _eventService = eventService;
        _mailService = mailService;
        _organizationRepository = organizationRepository;
        _organizationService = organizationService;
        _providerOrganizationRepository = providerOrganizationRepository;
    }

    public async Task RemoveOrganizationFromProvider(
        Provider provider,
        ProviderOrganization providerOrganization,
        Organization organization)
    {
        if (provider == null ||
            providerOrganization == null ||
            organization == null ||
            providerOrganization.ProviderId != provider.Id)
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

        var organizationOwnerEmails =
            (await _organizationRepository.GetOwnerEmailAddressesById(organization.Id)).ToList();

        organization.BillingEmail = organizationOwnerEmails.MinBy(email => email);

        await _organizationRepository.ReplaceAsync(organization);

        await _mailService.SendProviderUpdatePaymentMethod(
            organization.Id,
            organization.Name,
            provider.Name,
            organizationOwnerEmails);

        await _providerOrganizationRepository.DeleteAsync(providerOrganization);

        await _eventService.LogProviderOrganizationEventAsync(
            providerOrganization,
            EventType.ProviderOrganization_Removed);
    }
}
