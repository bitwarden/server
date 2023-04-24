using Bit.Core.Auth.Entities;
using Bit.Core.Entities;

namespace Bit.Core.Auth.Services;

public interface ISsoConfigService
{
    Task SaveAsync(SsoConfig config, Organization organization);
}
