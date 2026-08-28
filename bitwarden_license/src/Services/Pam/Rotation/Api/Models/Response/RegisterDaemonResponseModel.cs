using Bit.HttpExtensions;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.Rotation.Api.Models.Response;

/// <summary>The response to <c>POST rotation/daemons</c> (spec <c>DaemonRegistration</c>).</summary>
public class RegisterDaemonResponseModel : ResponseModel
{
    public RegisterDaemonResponseModel()
        : base("pamDaemon")
    {
    }

    /// <summary>
    /// The daemon's unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The organization the daemon was registered in.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The daemon's display label, as supplied at registration.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Whether the daemon may authenticate, poll, and claim jobs -- see <see cref="PamDaemonStatus"/>.
    /// </summary>
    public PamDaemonStatus Status { get; set; }

    /// <summary>
    /// When the daemon was registered (UTC).
    /// </summary>
    public DateTime CreationDate { get; set; }

    /// <summary>
    /// The id of the daemon's <c>dbo.ApiKey</c> credential. The operator assembles the daemon's OAuth client id
    /// from it (<c>daemon.&lt;ApiKeyId&gt;</c>, resolved server-side by <c>PamDaemonClientProvider</c> in Identity).
    /// </summary>
    public Guid ApiKeyId { get; set; }

    /// <summary>
    /// WARNING: shown exactly once. The plaintext client secret for the daemon's credential -- store it now; the
    /// server hashes it for storage and never persists or returns the plaintext again. Pair with the client-wrapped
    /// org key you already hold locally to assemble the daemon's token (<c>0.daemon.&lt;apiKeyId&gt;.&lt;client_secret&gt;:&lt;encryption_key&gt;</c>).
    /// </summary>
    public string ClientSecret { get; set; } = null!;
}
