using System.ComponentModel.DataAnnotations;
using Bit.Commercial.Pam.Models;
using Bit.Pam.Enums;

namespace Bit.Commercial.Pam.Api.Models.Request;

/// <summary>
/// An approver's decision on a pending access request. <see cref="Verdict"/> is the
/// <see cref="AccessDecisionVerdict"/> value on the wire (<c>0</c> = deny, <c>1</c> = approve);
/// <see cref="Comment"/> is optional.
/// </summary>
public class AccessDecisionRequestModel
{
    [Required]
    [EnumDataType(typeof(AccessDecisionVerdict))]
    public AccessDecisionVerdict? Verdict { get; set; }

    public string? Comment { get; set; }

    public AccessDecisionSubmission ToSubmission() => new()
    {
        Verdict = Verdict!.Value,
        Comment = Comment,
    };
}
