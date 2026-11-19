using Bit.Services.Pam.AccessConnector.Models;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IRegisterAccessConnectorCommand
{
    /// <summary>
    /// Registers a new access connector with a <c>dbo.ApiKey</c> credential scoped to <c>api.pam.rotation</c>.
    /// <paramref name="encryptedPayload"/> and <paramref name="key"/> arrive wrapped client-side; the client secret is
    /// stored only as a hash.
    /// </summary>
    Task<PamAccessConnectorRegistrationResult> RegisterAsync(
        Guid organizationId, Guid actingUserId, string name, string encryptedPayload, string key);
}
