using System.Text.Json;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Models.Api;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class PolicyStatusResponseModel : ResponseModel
{
    public PolicyStatusResponseModel(PolicyData policy, bool canToggleState = true) : base("policy")
    {
        OrganizationId = policy.OrganizationId;
        Type = policy.Type;

        if (!string.IsNullOrWhiteSpace(policy.Data))
        {
            Data = JsonSerializer.Deserialize<Dictionary<string, object>>(policy.Data) ?? new();
        }

        Enabled = policy.Enabled;
        CanToggleState = canToggleState;
    }

    public Guid OrganizationId { get; init; }
    public PolicyType Type { get; init; }
    public Dictionary<string, object> Data { get; init; } = new();
    public bool Enabled { get; init; }

    /// <summary>
    /// Indicates whether the Policy can be enabled/disabled
    /// </summary>
    public bool CanToggleState { get; init; }
}
