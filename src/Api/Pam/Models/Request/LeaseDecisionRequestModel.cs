using System.ComponentModel.DataAnnotations;
using Bit.Core.Exceptions;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;

namespace Bit.Api.Pam.Models.Request;

/// <summary>
/// An approver's decision on a pending lease request. <see cref="Decision"/> is <c>"approve"</c> or <c>"deny"</c>;
/// <see cref="Comment"/> is optional.
/// </summary>
public class LeaseDecisionRequestModel
{
    [Required]
    public string Decision { get; set; } = null!;

    public string? Comment { get; set; }

    public LeaseDecisionSubmission ToSubmission() => new()
    {
        Verdict = ParseVerdict(Decision),
        Comment = Comment,
    };

    private static LeaseDecisionVerdict ParseVerdict(string decision) => decision?.ToLowerInvariant() switch
    {
        "approve" => LeaseDecisionVerdict.Approve,
        "deny" => LeaseDecisionVerdict.Deny,
        _ => throw new BadRequestException("Decision must be either 'approve' or 'deny'."),
    };
}
