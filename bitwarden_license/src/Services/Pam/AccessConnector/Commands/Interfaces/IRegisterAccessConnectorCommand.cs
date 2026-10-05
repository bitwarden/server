using Bit.Services.Pam.AccessConnector.Models;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IRegisterAccessConnectorCommand
{
    /// <summary>
    /// Registers a new access connector: mints a <c>dbo.ApiKey</c> credential scoped to <c>api.pam.rotation</c>
    /// and a <see cref="Bit.Pam.Entities.PamAccessConnector"/> row referencing it. <paramref name="encryptedPayload"/>
    /// and <paramref name="key"/> are the client-wrapped org key (zero-knowledge); the returned client secret
    /// is surfaced only in the response.
    /// </summary>
    Task<PamAccessConnectorRegistrationResult> RegisterAsync(
        Guid organizationId, Guid actingUserId, string name, string encryptedPayload, string key);
}
