using Bit.Core.Vault.Enums;

namespace Bit.Api.Models.Request;

public class BulkCreateSecurityTasksRequestModel
{
    public SecurityTaskType Type { get; set; }
    public Guid CipherId { get; set; }
}
