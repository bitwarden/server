using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Api.Models.Public.Response;

/// <summary>
/// A policy.
/// </summary>
public class PolicyResponseModel : PolicyBaseModel, IResponseModel
{
    public PolicyResponseModel(Policy policy)
    {
        if (policy == null)
        {
            throw new ArgumentNullException(nameof(policy));
        }

        Id = policy.Id;
        Type = policy.Type;
        Enabled = policy.Enabled;
        if (!string.IsNullOrWhiteSpace(policy.Data))
        {
            Data = JsonSerializer.Deserialize<Dictionary<string, object>>(policy.Data);
        }
    }

    /// <summary>
    /// String representing the object's type. Objects of the same type share the same properties.
    /// </summary>
    /// <example>policy</example>
    [Required]
    public string Object => "policy";
    /// <summary>
    /// The policy's unique identifier.
    /// </summary>
    /// <example>539a36c5-e0d2-4cf9-979e-51ecf5cf6593</example>
    [Required]
    public Guid Id { get; set; }
    /// <summary>
    /// The type of policy.
    /// </summary>
    [Required]
    public PolicyType? Type { get; set; }
}
