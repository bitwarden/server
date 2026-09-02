using Bit.HttpExtensions;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.AccessConnector.Api.Models.Response;

/// <summary>The response to <c>POST access-connectors</c> (spec <c>ConnectorRegistration</c>).</summary>
public class RegisterAccessConnectorResponseModel : ResponseModel
{
    public RegisterAccessConnectorResponseModel()
        : base("pamAccessConnector")
    {
    }

    /// <summary>
    /// The access connector's unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The organization the access connector was registered in.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The access connector's display label, as supplied at registration.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Whether the access connector may authenticate, poll, and claim jobs -- see
    /// <see cref="PamAccessConnectorStatus"/>.
    /// </summary>
    public PamAccessConnectorStatus Status { get; set; }

    /// <summary>
    /// When the access connector was registered (UTC).
    /// </summary>
    public DateTime CreationDate { get; set; }

    /// <summary>
    /// The id of the access connector's <c>dbo.ApiKey</c> credential. The operator assembles the access connector's
    /// OAuth client id from it (<c>daemon.&lt;ApiKeyId&gt;</c>, resolved server-side by
    /// <c>PamAccessConnectorClientProvider</c> in Identity).
    /// </summary>
    public Guid ApiKeyId { get; set; }

    /// <summary>
    /// WARNING: shown exactly once. The plaintext client secret for the access connector's credential -- store it now;
    /// the server hashes it for storage and never persists or returns the plaintext again. Pair with the client-wrapped
    /// org key you already hold locally to assemble the access connector's token
    /// (<c>0.daemon.&lt;apiKeyId&gt;.&lt;client_secret&gt;:&lt;encryption_key&gt;</c>).
    /// </summary>
    public string ClientSecret { get; set; } = null!;
}
