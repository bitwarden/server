using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class PolicyDetailResponseModel : PolicyResponseModel
{
    public PolicyDetailResponseModel(PolicyData policy, bool canToggleState = true) : base(new Policy
    {
        OrganizationId = policy.OrganizationId,
        Data = policy.Data,
        Enabled = policy.Enabled,
        Type = policy.Type,
        Id = Guid.Empty
    })
    {
        CanToggleState = canToggleState;
    }

    /// <summary>
    /// Indicates whether the Policy can be enabled/disabled
    /// </summary>
    public bool CanToggleState { get; set; }
}
