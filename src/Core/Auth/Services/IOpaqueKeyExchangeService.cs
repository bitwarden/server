using Bit.Core.Entities;
using Bitwarden.OPAQUE;

namespace Bit.Core.Auth.Services;

public interface IOpaqueKeyExchangeService
{
    public Task<(Guid, byte[])> StartRegistration(byte[] request, User user, CipherConfiguration cipherConfiguration);
    public void FinishRegistration(Guid sessionId, byte[] registrationUpload, User user);
    public Task<(Guid, byte[])> StartLogin(byte[] request, string email);
    public Task<bool> FinishLogin(Guid sessionId, byte[] finishCredential);
    public void SetActive(Guid sessionId, User user);
    public void Unenroll(User user);
}
