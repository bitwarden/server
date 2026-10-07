using Bit.Pam.Enums;

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
