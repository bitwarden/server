using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;

namespace Bit.Core.Services
{
    public class SsoConfigService : ISsoConfigService
    {
        private readonly ISsoConfigRepository _ssoConfigRepository;

        public SsoConfigService(
            ISsoConfigRepository ssoConfigRepository)
        {
            _ssoConfigRepository = ssoConfigRepository;
        }

        public async Task SaveAsync(SsoConfig config)
        {
            var now = DateTime.UtcNow;
            config.RevisionDate = now;
            if (config.Id == default)
            {
                config.CreationDate = now;
            }
            await _ssoConfigRepository.UpsertAsync(config);
        }
    }
}
