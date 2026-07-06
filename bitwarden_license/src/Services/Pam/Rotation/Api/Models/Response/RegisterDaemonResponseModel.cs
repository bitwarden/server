using Bit.HttpExtensions;
using Bit.Pam.Enums;
using Bit.Services.Pam.Api.Models.Response;
using Bit.Services.Pam.Rotation.Models;

namespace Bit.Services.Pam.Rotation.Api.Models.Response;

/// <summary>The response to <c>POST rotation/daemons</c> (spec <c>DaemonRegistration</c>).</summary>
public class RegisterDaemonResponseModel : ResponseModel
{
    public RegisterDaemonResponseModel(PamDaemonRegistrationResult result)
        : base("pamDaemon")
    {
        ArgumentNullException.ThrowIfNull(result);

        Id = result.Daemon.Id;
        OrganizationId = result.Daemon.OrganizationId;
        Name = result.Daemon.Name;
        Status = result.Daemon.Status;
        CreationDate = result.Daemon.CreationDate.AsUtc();
        ApiKeyId = result.Daemon.ApiKeyId;
        ClientSecret = result.ClientSecret;
    }

    public Guid Id { get; }
    public Guid OrganizationId { get; }
    public string Name { get; }
    public PamDaemonStatus Status { get; }
    public DateTime CreationDate { get; }

    /// <summary>
    /// The id of the daemon's <c>dbo.ApiKey</c> credential. Required by the operator to assemble the daemon's
    /// OAuth client id (<c>daemon.&lt;ApiKeyId&gt;</c>, resolved server-side by <c>PamDaemonClientProvider</c> in
    /// Identity) -- without it there is no way to derive the client id from the admin API surface.
    /// </summary>
    public Guid ApiKeyId { get; }

    /// <summary>
    /// WARNING: shown exactly once. The plaintext client secret for the daemon's credential -- store it now; the
    /// server hashes it for storage and never persists or returns the plaintext again. Pair with the client-wrapped
    /// org key you already hold locally to assemble the daemon's token (<c>0.daemon.&lt;apiKeyId&gt;.&lt;client_secret&gt;:&lt;encryption_key&gt;</c>).
    /// </summary>
    public string ClientSecret { get; }
}
