using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;

namespace Bit.Core.Services
{
    public class SsoConfigService : ISsoConfigService
    {
        private readonly ISsoConfigRepository _ssoConfigRepository;
        private readonly IPolicyRepository _policyRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IEventService _eventService;

        public SsoConfigService(
            ISsoConfigRepository ssoConfigRepository,
            IPolicyRepository policyRepository,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IEventService eventService)
        {
            _ssoConfigRepository = ssoConfigRepository;
            _policyRepository = policyRepository;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _eventService = eventService;
        }

        public async Task SaveAsync(SsoConfig config)
        {
            var now = DateTime.UtcNow;
            config.RevisionDate = now;
            if (config.Id == default)
            {
                config.CreationDate = now;
            }

            var useKeyConnector = config.GetData().UseKeyConnector;
            if (useKeyConnector)
            {
                await VerifyDependenciesAsync(config);
            }

            var oldConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(config.OrganizationId);
            var disabledKeyConnector = oldConfig?.GetData()?.UseKeyConnector == true && !useKeyConnector;
            if (disabledKeyConnector && await AnyOrgUserHasKeyConnectorEnabledAsync(config.OrganizationId))
            {
                throw new BadRequestException("Key Connector cannot be disabled at this moment.");
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

        private async Task VerifyDependenciesAsync(SsoConfig config)
        {
            var policy = await _policyRepository.GetByOrganizationIdTypeAsync(config.OrganizationId, PolicyType.SingleOrg);
            if (policy is not { Enabled: true })
            {
                throw new BadRequestException("KeyConnector requires Single Organization to be enabled.");
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

            var useKeyConnector = config.GetData().UseKeyConnector;
            if (oldConfig?.GetData()?.UseKeyConnector != useKeyConnector)
            {
                var e = useKeyConnector
                    ? EventType.Organization_EnabledKeyConnector
                    : EventType.Organization_DisabledKeyConnector;
                await _eventService.LogOrganizationEventAsync(organization, e);
            }
        }
    }
}
