using Bit.Core.Entities;

namespace Bit.Core.Services;

public interface ISsoConfigService
{
    Task SaveAsync(SsoConfig config, Organization organization);
}
