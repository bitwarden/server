using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.OrganizationConnectionConfigs;

namespace Bit.Core.Models.Data.Organizations.OrganizationConnections;

public class OrganizationConnectionData<T>
    where T : IConnectionConfig
{
    public Guid? Id { get; set; }
    public OrganizationConnectionType Type { get; set; }
    public Guid OrganizationId { get; set; }
    public bool Enabled { get; set; }
    public T Config { get; set; }

    public OrganizationConnection ToEntity()
    {
        var result = new OrganizationConnection()
        {
            Type = Type,
            OrganizationId = OrganizationId,
            Enabled = Enabled,
        };
        result.SetConfig(Config);

        if (Id.HasValue)
        {
            result.Id = Id.Value;
        }

        return result;
    }
}
