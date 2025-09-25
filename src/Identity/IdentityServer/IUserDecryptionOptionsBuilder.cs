// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Response;
using Bit.Core.Entities;

namespace Bit.Identity.IdentityServer;
public interface IUserDecryptionOptionsBuilder
{
    IUserDecryptionOptionsBuilder ForUser(User user);
    IUserDecryptionOptionsBuilder WithDevice(Device device);
    IUserDecryptionOptionsBuilder WithSso(SsoConfig ssoConfig);
    IUserDecryptionOptionsBuilder WithWebAuthnLoginCredentials(IEnumerable<WebAuthnCredential> credentials);
    Task<UserDecryptionOptions> BuildAsync();
}
