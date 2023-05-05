using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.Policies;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.Auth.Services;

public class SsoConfigService : ISsoConfigService
{
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly IPolicyService _policyService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IUserService _userService;
    private readonly IOrganizationService _organizationService;
    private readonly IEventService _eventService;

    public SsoConfigService(
        ISsoConfigRepository ssoConfigRepository,
        IPolicyRepository policyRepository,
        IPolicyService policyService,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IUserService userService,
        IOrganizationService organizationService,
        IEventService eventService)
    {
        _ssoConfigRepository = ssoConfigRepository;
        _policyRepository = policyRepository;
        _policyService = policyService;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _userService = userService;
        _organizationService = organizationService;
        _eventService = eventService;
    }

    public async Task SaveAsync(SsoConfig config, Organization organization)
    {
        var now = DateTime.UtcNow;
        config.RevisionDate = now;
        if (config.Id == default)
        {
            config.CreationDate = now;
        }

        var useKeyConnector = config.GetData().MemberDecryptionType == MemberDecryptionType.KeyConnector;
        if (useKeyConnector)
        {
            await VerifyDependenciesAsync(config, organization);
        }

        var oldConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(config.OrganizationId);
        var disabledKeyConnector = oldConfig?.GetData()?.MemberDecryptionType == MemberDecryptionType.KeyConnector && !useKeyConnector;
        if (disabledKeyConnector && await AnyOrgUserHasKeyConnectorEnabledAsync(config.OrganizationId))
        {
            throw new BadRequestException("Key Connector cannot be disabled at this moment.");
        }

        // Automatically enable reset password policy if trusted device encryption is selected
        if (config.GetData().MemberDecryptionType == MemberDecryptionType.TrustedDeviceEncryption)
        {
            var resetPolicy = await _policyRepository.GetByOrganizationIdTypeAsync(config.OrganizationId, PolicyType.ResetPassword) ??
                              new Policy { OrganizationId = config.OrganizationId, Type = PolicyType.ResetPassword, };

            resetPolicy.Enabled = true;
            resetPolicy.SetDataModel(new ResetPasswordDataModel { AutoEnrollEnabled = true });

            await _policyService.SaveAsync(resetPolicy, _userService, _organizationService, null);
        }

        await LogEventsAsync(config, oldConfig);
        await _ssoConfigRepository.UpsertAsync(config);
    }

    private async Task<bool> AnyOrgUserHasKeyConnectorEnabledAsync(Guid organizationId)
    {
        var userDetails =
            await _organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);
        return userDetails.Any(u => u.UsesKeyConnector);
    }

    private async Task VerifyDependenciesAsync(SsoConfig config, Organization organization)
    {
        if (!organization.UseKeyConnector)
        {
            throw new BadRequestException("Organization cannot use Key Connector.");
        }

        var singleOrgPolicy = await _policyRepository.GetByOrganizationIdTypeAsync(config.OrganizationId, PolicyType.SingleOrg);
        if (singleOrgPolicy is not { Enabled: true })
        {
            throw new BadRequestException("Key Connector requires the Single Organization policy to be enabled.");
        }

        var ssoPolicy = await _policyRepository.GetByOrganizationIdTypeAsync(config.OrganizationId, PolicyType.RequireSso);
        if (ssoPolicy is not { Enabled: true })
        {
            throw new BadRequestException("Key Connector requires the Single Sign-On Authentication policy to be enabled.");
        }

        if (!config.Enabled)
        {
            throw new BadRequestException("You must enable SSO to use Key Connector.");
        }
    }

    private async Task LogEventsAsync(SsoConfig config, SsoConfig oldConfig)
    {
        var organization = await _organizationRepository.GetByIdAsync(config.OrganizationId);
        if (oldConfig?.Enabled != config.Enabled)
        {
            var e = config.Enabled ? EventType.Organization_EnabledSso : EventType.Organization_DisabledSso;
            await _eventService.LogOrganizationEventAsync(organization, e);
        }

        var keyConnectorEnabled = config.GetData().MemberDecryptionType == MemberDecryptionType.KeyConnector;
        var oldKeyConnectorEnabled = oldConfig?.GetData()?.MemberDecryptionType == MemberDecryptionType.KeyConnector;
        if (oldKeyConnectorEnabled != keyConnectorEnabled)
        {
            var e = keyConnectorEnabled
                ? EventType.Organization_EnabledKeyConnector
                : EventType.Organization_DisabledKeyConnector;
            await _eventService.LogOrganizationEventAsync(organization, e);
        }
    }
}
