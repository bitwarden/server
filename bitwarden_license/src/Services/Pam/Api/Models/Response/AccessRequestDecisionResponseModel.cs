// The decider kind is the one wire enum that keeps its domain type: the published bindings generate it as
// `DeciderKind` while the domain calls it `AccessDeciderKind`, and renaming either side would move the generated
// schema. The two carry identical values, so the wire bytes are the same whichever type declares the property.
using AccessDeciderKind = Bit.Pam.Enums.AccessDeciderKind;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>One decision in <see cref="AccessRequestDetailsResponseModel.Decisions"/>.</summary>
public class AccessRequestDecisionResponseModel
{
    public AccessDeciderKind DeciderKind { get; set; }

    /// <summary>The human approver's user id, or null for an automatic decision.</summary>
    public Guid? Id { get; set; }

    /// <summary>The approver's display name; null for an automatic decision or an unresolved user.</summary>
    public string? Name { get; set; }

    /// <summary>The approver's email; null for an automatic decision or an unresolved user.</summary>
    public string? Email { get; set; }

    public string? Comment { get; set; }

    public AccessDecisionVerdict Verdict { get; set; }

    public DateTime DecidedAt { get; set; }
}
