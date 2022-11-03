using Bit.Core.Entities;

namespace Bit.Core.Models.Data;

public class ApiKeyDetails : ApiKey
{
    public ApiKeyDetails() { }

    public Guid ServiceAccountOrganizationId { get; set; }
}
