using Bit.Core.Vault.Models.Api;

namespace Bit.Api.Vault.Models.Request;

public class BulkCreateSecurityTasksRequestModel
{
    public IEnumerable<SecurityTaskCreateRequest> Tasks { get; set; }
}
