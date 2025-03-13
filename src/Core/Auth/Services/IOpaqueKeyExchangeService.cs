using Bit.Core.Entities;
using Bitwarden.OPAQUE;

namespace Bit.Core.Auth.Services;

public interface IOpaqueKeyExchangeService
{
    public Task<(Guid, byte[])> StartRegistration(byte[] request, User user, CipherConfiguration cipherConfiguration);
    public Task<bool> FinishRegistration(Guid sessionId, byte[] request, User user);
}
