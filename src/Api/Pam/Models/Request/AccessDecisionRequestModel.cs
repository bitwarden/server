using System.ComponentModel.DataAnnotations;
using Bit.Core.Exceptions;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;

namespace Bit.Api.Pam.Models.Request;

/// <summary>
/// An approver's decision on a pending access request. <see cref="Verdict"/> is <c>"approve"</c> or <c>"deny"</c>;
/// <see cref="Comment"/> is optional.
/// </summary>
public class AccessDecisionRequestModel
{
    [Required]
    public string Verdict { get; set; } = null!;

    public string? Comment { get; set; }

    public AccessDecisionSubmission ToSubmission() => new()
    {
        Verdict = ParseVerdict(Verdict),
        Comment = Comment,
    };

    private static AccessDecisionVerdict ParseVerdict(string verdict) => verdict?.ToLowerInvariant() switch
    {
        "approve" => AccessDecisionVerdict.Approve,
        "deny" => AccessDecisionVerdict.Deny,
        _ => throw new BadRequestException("Verdict must be either 'approve' or 'deny'."),
    };
}
