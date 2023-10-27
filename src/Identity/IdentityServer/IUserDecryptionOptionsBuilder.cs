using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Response;
using Bit.Core.Entities;

namespace Bit.Identity.IdentityServer;
public interface IUserDecryptionOptionsBuilder
{
    Task<UserDecryptionOptions> BuildAsync();
    IUserDecryptionOptionsBuilder ForUser(User user);
    IUserDecryptionOptionsBuilder WithDevice(Device device);
    IUserDecryptionOptionsBuilder WithSso(SsoConfig ssoConfig);
}