using Bit.Core.Entities;
using Bitwarden.OPAQUE;
using Bit.Core.Auth.Models.Api.Request.Opaque;
using Bit.Core.Auth.Models.Api.Response.Opaque;

namespace Bit.Core.Auth.Services;

public interface IOpaqueKeyExchangeService
{
    public Task<OpaqueRegistrationStartResponse> StartRegistration(byte[] request, User user, CipherConfiguration cipherConfiguration);
    public Task<bool> FinishRegistration(Guid sessionId, byte[] clientSetup, RotateableOpaqueKeyset keys, User user);
}
