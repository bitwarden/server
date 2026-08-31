using Bit.Services.Pam.AccessConnector.Models;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IRegisterAccessConnectorCommand
{
    /// <summary>
    /// Registers a new rotation daemon (spec <c>DaemonRegistration</c>): mints a <c>dbo.ApiKey</c> credential scoped
    /// to <c>api.pam.rotation</c> and a <see cref="Bit.Pam.Entities.PamDaemon"/> row referencing it.
    /// <paramref name="encryptedPayload"/> and <paramref name="key"/> are the client-wrapped org key (zero-knowledge
    /// — the server never sees the plaintext key); the returned client secret is surfaced exactly once.
    /// </summary>
    Task<PamAccessConnectorRegistrationResult> RegisterAsync(
        Guid organizationId, Guid actingUserId, string name, string encryptedPayload, string key);
}
