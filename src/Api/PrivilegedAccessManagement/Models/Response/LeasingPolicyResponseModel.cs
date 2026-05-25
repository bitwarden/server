using System.Text.Json;
using Bit.Core.Models.Api;
using Bit.Core.PrivilegedAccessManagement.Entities;

namespace Bit.Api.PrivilegedAccessManagement.Models.Response;

public class LeasingPolicyResponseModel : ResponseModel
{
    public LeasingPolicyResponseModel(LeasingPolicy policy)
        : base("leasingPolicy")
    {
        ArgumentNullException.ThrowIfNull(policy);

        Id = policy.Id;
        OrganizationId = policy.OrganizationId;
        Name = policy.Name;
        Description = policy.Description;
        Policy = TryParsePolicy(policy.Policy);
        CreationDate = policy.CreationDate;
        RevisionDate = policy.RevisionDate;
    }

    public Guid Id { get; }
    public Guid OrganizationId { get; }
    public string Name { get; }
    public string? Description { get; }
    public JsonElement? Policy { get; }
    public DateTime CreationDate { get; }
    public DateTime RevisionDate { get; }

    private static JsonElement? TryParsePolicy(string? policyJson)
    {
        if (string.IsNullOrEmpty(policyJson))
        {
            return null;
        }
        try
        {
            return JsonDocument.Parse(policyJson).RootElement;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
