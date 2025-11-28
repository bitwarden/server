using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;

namespace Bit.Core.Auth.Services;

public interface ISsoConfigService
{
    Task SaveAsync(SsoConfig config, Organization organization);
}
