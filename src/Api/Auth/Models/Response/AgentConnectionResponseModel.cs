#nullable enable

using Bit.Core.Auth.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response;

public class AgentConnectionResponseModel : ResponseModel
{
    public AgentConnectionResponseModel(AgentConnection connection, string obj = "agentConnection")
        : base(obj)
    {
        ArgumentNullException.ThrowIfNull(connection);

        Id = connection.Id;
        PartnerClientId = connection.PartnerClientId;
        Name = connection.Name;
        Key = connection.Key;
        CreationDate = connection.CreationDate;
        RevisionDate = connection.RevisionDate;
        RevokedDate = connection.RevokedDate;
    }

    public Guid Id { get; set; }
    public string PartnerClientId { get; set; }
    public string Name { get; set; }

    /// <summary>The user-key-encrypted PSK, returned for clients to decrypt locally.</summary>
    public string Key { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }
    public DateTime? RevokedDate { get; set; }
}
