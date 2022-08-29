using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Models.Data.Organizations.OrganizationConnections;

public class OrganizationConnectionData<T> where T : new()
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
