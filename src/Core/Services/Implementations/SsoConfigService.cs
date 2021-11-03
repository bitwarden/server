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

        public SsoConfigService(
            ISsoConfigRepository ssoConfigRepository,
            IPolicyRepository policyRepository)
        {
            _ssoConfigRepository = ssoConfigRepository;
            _policyRepository = policyRepository;
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

            await _ssoConfigRepository.UpsertAsync(config);
        }
    }
}
