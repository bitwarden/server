using Bit.Core.Auth.Models.Api.Request.Opaque;
using Bit.Core.Auth.Models.Api.Response.Opaque;
using Bit.Core.Entities;

namespace Bit.Core.Auth.Services;

public interface IOpaqueKeyExchangeService
{
    public Task<OpaqueRegistrationStartResponse> StartRegistration(byte[] request, User user, Bitwarden.OPAQUE.CipherConfiguration cipherConfiguration);
    public void FinishRegistration(Guid sessionId, byte[] registrationUpload, User user, RotateableOpaqueKeyset keyset);
    public Task<(Guid, byte[])> StartLogin(byte[] request, string email);
    public Task<bool> FinishLogin(Guid sessionId, byte[] finishCredential);
    public void SetActive(Guid sessionId, User user);
    public void Unenroll(User user);
}
