using Bit.Core.Vault.Enums;

namespace Bit.Api.Models.Request;

public class BulkCreateSecurityTasksRequestModel
{
    public IEnumerable<SecurityTaskCreateRequest> Tasks { get; set; }
}
