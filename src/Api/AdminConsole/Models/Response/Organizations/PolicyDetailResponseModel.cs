using Bit.Core.AdminConsole.Entities;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class PolicyDetailResponseModel : PolicyResponseModel
{
    public PolicyDetailResponseModel(Policy policy, string obj = "policy")
        : base(policy, obj) { }

    public PolicyDetailResponseModel(Policy policy, bool canToggleState)
        : base(policy)
    {
        CanToggleState = canToggleState;
    }

    /// <summary>
    /// Indicates whether the Policy can be enabled/disabled
    /// </summary>
    public bool CanToggleState { get; set; } = true;
}
