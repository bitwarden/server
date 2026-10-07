using Bit.HttpExtensions;
using Bit.Pam.Enums;
using Bit.Services.Pam.AccessConnector.Models;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Api.Models.Response;

/// <summary>The response to <c>POST access-connectors</c> (spec <c>ConnectorRegistration</c>).</summary>
public class RegisterAccessConnectorResponseModel : ResponseModel
{
    public RegisterAccessConnectorResponseModel(PamAccessConnectorRegistrationResult result)
        : base("pamAccessConnector")
    {
        ArgumentNullException.ThrowIfNull(result);

        Id = result.AccessConnector.Id;
        OrganizationId = result.AccessConnector.OrganizationId;
        Name = result.AccessConnector.Name;
        Status = result.AccessConnector.Status;
        CreationDate = result.AccessConnector.CreationDate.AsUtc();
        ApiKeyId = result.AccessConnector.ApiKeyId;
        ClientSecret = result.ClientSecret;
    }

    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = null!;

    public PamAccessConnectorStatus Status { get; set; }

    public DateTime CreationDate { get; set; }

    /// <summary>The access connector's OAuth client id is <c>access-connector.&lt;ApiKeyId&gt;</c>.</summary>
    public Guid ApiKeyId { get; set; }

    /// <summary>
    /// WARNING: shown exactly once; the server stores only its hash. Combine it with the client-side encryption key to
    /// form the token, <c>0.access-connector.&lt;apiKeyId&gt;.&lt;client_secret&gt;:&lt;encryption_key&gt;</c>.
    /// </summary>
    public string ClientSecret { get; set; } = null!;
}
