using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;

namespace Bit.Api.AdminConsole.Public.Models.Response;

public class PolicyDetailResponseModel : PolicyResponseModel
{
    public PolicyDetailResponseModel(Policy policy) : base(policy)
    {
    }

    public PolicyDetailResponseModel(Policy policy, bool canToggleState) : this(policy)
    {
        CanToggleState = canToggleState;
    }

    /// <summary>
    /// Indicates whether the Policy can be enabled/disabled
    /// </summary>
    [Required]
    public bool CanToggleState { get; set; } = true;
}
