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

            if (config.GetData().UseCryptoAgent)
            {
                var policy = await _policyRepository.GetByOrganizationIdTypeAsync(config.OrganizationId, PolicyType.SingleOrg);
                if (policy is not { Enabled: true })
                {
                    throw new BadRequestException("CryptoAgent requires Single Organization to be enabled");
                }
            }

            var oldConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(config.OrganizationId);
            var organization = await _organizationRepository.GetByIdAsync(config.OrganizationId);
            if (oldConfig?.Enabled != config.Enabled)
            {
                var e = config.Enabled ? EventType.Organization_EnabledSso : EventType.Organization_DisabledSso;
                await _eventService.LogOrganizationEventAsync(organization, e);
            }

            if (oldConfig?.GetData()?.UseCryptoAgent != config.GetData().UseCryptoAgent)
            {
                var e = config.GetData().UseCryptoAgent ? EventType.Organization_EnabledKeyConnector : EventType.Organization_DisabledKeyConnector;
                await _eventService.LogOrganizationEventAsync(organization, e);
            }

            await _ssoConfigRepository.UpsertAsync(config);
        }
    }
}
