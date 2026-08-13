using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Models.Api;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class PolicyStatusResponseModel : ResponseModel
{
    public PolicyStatusResponseModel(PolicyStatus policy, bool canToggleState = true) : base("policy")
    {
        OrganizationId = policy.OrganizationId;
        Type = policy.Type;
        Data = string.IsNullOrWhiteSpace(policy.Data) ? null : policy.Data;
        Enabled = policy.Enabled;
        CanToggleState = canToggleState;
    }

    public Guid OrganizationId { get; init; }
    public PolicyType Type { get; init; }

    [JsonConverter(typeof(RawJsonConverter))]
    public string? Data { get; init; }
    public bool Enabled { get; init; }

    /// <summary>
    /// Indicates whether the Policy can be enabled/disabled
    /// </summary>
    public bool CanToggleState { get; init; }
}
