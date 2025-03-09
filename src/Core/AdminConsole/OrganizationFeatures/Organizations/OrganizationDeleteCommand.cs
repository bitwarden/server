﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public class OrganizationDeleteCommand : IOrganizationDeleteCommand
{
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IPaymentService _paymentService;
    private readonly IReferenceEventService _referenceEventService;
    private readonly ISsoConfigRepository _ssoConfigRepository;

    public OrganizationDeleteCommand(
        IApplicationCacheService applicationCacheService,
        ICurrentContext currentContext,
        IOrganizationRepository organizationRepository,
        IPaymentService paymentService,
        IReferenceEventService referenceEventService,
        ISsoConfigRepository ssoConfigRepository)
    {
        _applicationCacheService = applicationCacheService;
        _currentContext = currentContext;
        _organizationRepository = organizationRepository;
        _paymentService = paymentService;
        _referenceEventService = referenceEventService;
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
                await _referenceEventService.RaiseEventAsync(
                    new ReferenceEvent(ReferenceEventType.DeleteAccount, organization, _currentContext));
            }
            catch (GatewayException) { }
        }

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
