using System.ComponentModel.DataAnnotations;
using Bit.Pam.Enums;
using Bit.Services.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Request;

/// <summary>
/// An approver's decision on a pending access request: approve or deny, with a comment for the requester.
/// </summary>
public class AccessDecisionRequestModel
{
    /// <summary>
    /// The approver's verdict on the request: approve or deny.
    /// </summary>
    [Required]
    [EnumDataType(typeof(AccessDecisionVerdict))]
    public AccessDecisionVerdict? Verdict { get; set; }

    /// <summary>
    /// A note recorded with the decision — for example the reason for a denial. Surfaced to the requester.
    /// </summary>
    public string? Comment { get; set; }

    public AccessDecisionSubmission ToSubmission() => new()
    {
        Verdict = Verdict!.Value,
        Comment = Comment,
    };
}
