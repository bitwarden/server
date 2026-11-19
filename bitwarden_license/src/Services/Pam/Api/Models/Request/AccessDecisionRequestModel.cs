using System.ComponentModel.DataAnnotations;
using Bit.Pam.Enums;
using Bit.Services.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Request;

/// <summary>An approver's decision on a pending access request.</summary>
public class AccessDecisionRequestModel
{
    [Required]
    [EnumDataType(typeof(AccessDecisionVerdict))]
    public AccessDecisionVerdict? Verdict { get; set; }

    /// <summary>A note recorded with the decision, such as a denial's reason. Surfaced to the requester.</summary>
    public string? Comment { get; set; }

    public AccessDecisionSubmission ToSubmission() => new()
    {
        Verdict = Verdict!.Value,
        Comment = Comment,
    };
}
