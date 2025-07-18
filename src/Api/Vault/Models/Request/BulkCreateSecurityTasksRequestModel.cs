// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Vault.Models.Api;

namespace Bit.Api.Vault.Models.Request;

public class BulkCreateSecurityTasksRequestModel
{
    public IEnumerable<SecurityTaskCreateRequest> Tasks { get; set; }
}
