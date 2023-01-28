using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Api.Models.Response.Organizations;

public class OrganizationConnectionResponseModel
{
    public Guid? Id { get; set; }
    public OrganizationConnectionType Type { get; set; }
    public Guid OrganizationId { get; set; }
    public bool Enabled { get; set; }
    public JsonDocument Config { get; set; }

    public OrganizationConnectionResponseModel(OrganizationConnection connection, Type configType)
    {
        if (connection == null)
        {
            return;
        }

        Id = connection.Id;
        Type = connection.Type;
        OrganizationId = connection.OrganizationId;
        Enabled = connection.Enabled;
        Config = JsonDocument.Parse(connection.Config);
    }
}
