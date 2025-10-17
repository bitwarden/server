using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.Context;

namespace Bit.Api.AdminConsole.Models.Request;

public class PolicyRequestModel
{
    [Required]
    public PolicyType? Type { get; set; }
    [Required]
    public bool? Enabled { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();

    public async Task<PolicyUpdate> ToPolicyUpdateAsync(Guid organizationId, ICurrentContext currentContext)
    {
        var serializedData = PolicyDataValidator.ValidateAndSerialize(Data, Type!.Value);
        var performedBy = new StandardUser(currentContext.UserId!.Value, await currentContext.OrganizationOwner(organizationId));

        return new()
        {
            Type = Type!.Value,
            OrganizationId = organizationId,
            Data = serializedData,
            Enabled = Enabled.GetValueOrDefault(),
            PerformedBy = performedBy
        };
    }
}
