// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Api.Models.Public.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Utilities;
using Newtonsoft.Json;
using JsonConverterAttribute = System.Text.Json.Serialization.JsonConverterAttribute;

namespace Bit.Api.AdminConsole.Public.Models.Response;

/// <summary>
/// A policy.
/// </summary>
public class PolicyResponseModel : PolicyBaseModel, IResponseModel
{
    [JsonConstructor]
    public PolicyResponseModel() { }

    public PolicyResponseModel(Policy policy)
    {
        if (policy == null)
        {
            throw new ArgumentNullException(nameof(policy));
        }

        Id = policy.Id;
        Type = policy.Type;
        Enabled = policy.Enabled;
        Data = string.IsNullOrWhiteSpace(policy.Data) ? null : policy.Data;
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
    /// <summary>
    /// Data for the policy.
    /// </summary>
    [JsonConverter(typeof(RawJsonConverter))]
    public new string Data { get; set; }
}
