using System;
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
        private readonly IEventService _eventService;

        public SsoConfigService(
            ISsoConfigRepository ssoConfigRepository,
            IPolicyRepository policyRepository,
            IOrganizationRepository organizationRepository,
            IEventService eventService)
        {
            _ssoConfigRepository = ssoConfigRepository;
            _policyRepository = policyRepository;
            _organizationRepository = organizationRepository;
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
                var policy = await _policyRepository.GetByOrganizationIdTypeAsync(config.OrganizationId, PolicyType.SingleOrg);
                if (policy is not { Enabled: true })
                {
                    throw new BadRequestException("KeyConnector requires Single Organization to be enabled");
                }
            }

            var oldConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(config.OrganizationId);
            var oldUseKeyConnector = oldConfig?.GetData()?.UseKeyConnector == true;
            if (oldUseKeyConnector && !useKeyConnector)
            {
                throw new BadRequestException("KeyConnector cannot be disabled at this moment.");
            }

            var organization = await _organizationRepository.GetByIdAsync(config.OrganizationId);
            if (oldConfig?.Enabled != config.Enabled)
            {
                var e = config.Enabled ? EventType.Organization_EnabledSso : EventType.Organization_DisabledSso;
                await _eventService.LogOrganizationEventAsync(organization, e);
            }

            if (oldUseKeyConnector != useKeyConnector)
            {
                var e = useKeyConnector ? EventType.Organization_EnabledKeyConnector : EventType.Organization_DisabledKeyConnector;
                await _eventService.LogOrganizationEventAsync(organization, e);
            }

            await _ssoConfigRepository.UpsertAsync(config);
        }
    }
}
