using Bit.Core.Auth.Models.Api.Request.Opaque;
using Bit.Core.Auth.Models.Api.Response.Opaque;
using Bit.Core.Entities;

namespace Bit.Core.Auth.Services;

/// <summary>
/// Service that exposes methods enabling the use of the Opaque Key Exchange extension.
/// </summary>
public interface IOpaqueKeyExchangeService
{
    /// <summary>
    /// Begin registering a user's Opaque Key Exchange Credential. We write to the distributed cache so since there is some back and forth between the client and server.
    /// </summary>
    /// <param name="request">unsure what this byte array is for.</param>
    /// <param name="user">user being acted on</param>
    /// <param name="cipherConfiguration">configuration shared between the client and server to ensure the proper crypto-algorithms are being utilized.</param>
    /// <returns>void</returns>
    public Task<OpaqueRegistrationStartResponse> StartRegistration(byte[] request, User user, OpaqueKeyExchangeCipherConfiguration cipherConfiguration);
    /// <summary>
    /// This doesn't actually finish registration. It updates the cache with the server setup and cipher configuration so that the clearly named "SetActive" method can finish registration.
    /// </summary>
    /// <param name="sessionId">Cache Id</param>
    /// <param name="registrationUpload">Byte Array for Rust Magic</param>
    /// <param name="user">User being acted on</param>
    /// <param name="keyset">Key Pair that can be used for vault decryption</param>
    /// <returns>void</returns>
    public Task FinishRegistration(Guid sessionId, byte[] registrationUpload, User user, RotateableOpaqueKeyset keyset);
    /// <summary>
    /// Returns server crypto material for the client to consume and reply with a login request to the identity/token endpoint.
    /// To protect against account enumeration we will always return a deterministic response based on the user's email.
    /// </summary>
    /// <param name="request">client crypto material</param>
    /// <param name="email">user email trying to login</param>
    /// <returns>tuple(login SessionId for cache lookup, Server crypto material)</returns>
    public Task<(Guid, byte[])> StartLogin(byte[] request, string email);
    /// <summary>
    /// Accepts the client's login request and validates it against the server's crypto material. If successful then the user is logged in.
    /// If using a fake account we will return a standard failed login. If the account does have a legitimate credential but is still invalid
    /// we will return a failed login.
    /// </summary>
    /// <param name="sessionId"></param>
    /// <param name="finishCredential"></param>
    /// <returns></returns>
    public Task<bool> FinishLogin(Guid sessionId, byte[] finishCredential);
    /// <summary>
    /// This is where registration really finishes. This method writes the Credential to the database. If a credential already exists then it will be removed before the new one is added.
    /// A user can only have one credential.
    /// </summary>
    /// <param name="sessionId">cache value</param>
    /// <param name="user">user being acted on</param>
    /// <returns>void</returns>
    public Task SetActive(Guid sessionId, User user);
    /// <summary>
    /// Removes the credential for the user.
    /// </summary>
    /// <param name="user">user being acted on</param>
    /// <returns>void</returns>
    public Task Unenroll(User user);
}
