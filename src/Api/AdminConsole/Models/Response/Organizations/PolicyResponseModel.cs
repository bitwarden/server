// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Models.Api;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class PolicyResponseModel : ResponseModel
{
    public PolicyResponseModel() : base("policy")
    {
    }

    public PolicyResponseModel(Policy policy, string obj = "policy")
        : base(obj)
    {
        if (policy == null)
        {
            throw new ArgumentNullException(nameof(policy));
        }

        Id = policy.Id;
        OrganizationId = policy.OrganizationId;
        Type = policy.Type;
        Enabled = policy.Enabled;
        if (!string.IsNullOrWhiteSpace(policy.Data))
        {
            Data = JsonSerializer.Deserialize<Dictionary<string, object>>(policy.Data);
        }
        RevisionDate = policy.RevisionDate;
    }

    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public PolicyType Type { get; set; }
    public Dictionary<string, object> Data { get; set; }
    public bool Enabled { get; set; }
    public DateTime RevisionDate { get; set; }
}
