using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response;

public class PolicyResponseModel : ResponseModel
{
    public PolicyResponseModel(Policy policy, string obj = "policy")
        : base(obj)
    {
        if (policy == null)
        {
            throw new ArgumentNullException(nameof(policy));
        }

        Id = policy.Id.ToString();
        OrganizationId = policy.OrganizationId.ToString();
        Type = policy.Type;
        Enabled = policy.Enabled;
        if (!string.IsNullOrWhiteSpace(policy.Data))
        {
            Data = JsonSerializer.Deserialize<Dictionary<string, object>>(policy.Data);
        }
    }

    public string Id { get; set; }
    public string OrganizationId { get; set; }
    public PolicyType Type { get; set; }
    public Dictionary<string, object> Data { get; set; }
    public bool Enabled { get; set; }
}
