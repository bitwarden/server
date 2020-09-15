using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;

namespace Bit.Core.Services
{
    public class SsoConfigService : ISsoConfigService
    {
        private readonly ISsoConfigRepository _ssoConfigRepository;

        public SsoConfigService(ISsoConfigRepository ssoConfigRepository)
        {
            _ssoConfigRepository = ssoConfigRepository;
        }

        public async Task SaveAsync(SsoConfig config)
        {
            if (config.Id == default)
            {
                config.RevisionDate = config.CreationDate = DateTime.UtcNow;
                await _ssoConfigRepository.CreateAsync(config);
            }
            else
            {
                config.RevisionDate = DateTime.UtcNow;
                await _ssoConfigRepository.ReplaceAsync(config);
            }
        }
    }
}
