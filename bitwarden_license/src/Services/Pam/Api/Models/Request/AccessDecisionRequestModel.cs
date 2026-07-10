using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.Api.Models.Request;

/// <summary>
/// An approver's decision on a pending access request. <see cref="Verdict"/> is the
/// <see cref="AccessDecisionVerdict"/> value on the wire (<c>0</c> = deny, <c>1</c> = approve);
/// <see cref="Comment"/> is optional.
/// </summary>
public class AccessDecisionRequestModel
{
    /// <summary>
    /// The approver's verdict on the request: deny (<c>0</c>) or approve (<c>1</c>). Required.
    /// </summary>
    [Required]
    [EnumDataType(typeof(AccessDecisionVerdict))]
    public AccessDecisionVerdict? Verdict { get; set; }

    /// <summary>
    /// An optional note recorded with the decision — for example the reason for a denial. Surfaced to the requester.
    /// </summary>
    public string? Comment { get; set; }
}
