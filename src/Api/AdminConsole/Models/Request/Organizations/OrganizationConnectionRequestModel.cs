using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationConnections;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationConnectionRequestModel
{
    public OrganizationConnectionType Type { get; set; }
    public Guid OrganizationId { get; set; }
    public bool Enabled { get; set; }
    public JsonDocument Config { get; set; }

    public OrganizationConnectionRequestModel() { }
}

public class OrganizationConnectionRequestModel<T> : OrganizationConnectionRequestModel
    where T : IConnectionConfig
{
    public T ParsedConfig { get; private set; }

    public OrganizationConnectionRequestModel(OrganizationConnectionRequestModel model)
    {
        Type = model.Type;
        OrganizationId = model.OrganizationId;
        Enabled = model.Enabled;
        Config = model.Config;

        try
        {
            ParsedConfig = model.Config.Deserialize<T>(JsonHelpers.IgnoreCase);
        }
        catch (JsonException)
        {
            throw new BadRequestException("Organization Connection configuration malformed");
        }
    }

    public OrganizationConnectionData<T> ToData(Guid? id = null) =>
        new()
        {
            Id = id,
            Type = Type,
            OrganizationId = OrganizationId,
            Enabled = Enabled,
            Config = ParsedConfig,
        };
}
