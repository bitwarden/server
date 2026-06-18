using Bit.Pam.Enums;
using Bit.Pam.Models;

namespace Bit.Api.Pam.Models.Response;

/// <summary>
/// One decision on an access request: who decided (<see cref="DeciderKind"/>), their identity for a human decision,
/// the verdict, an optional comment, and when. An element of <see cref="AccessRequestDetailsResponseModel.Decisions"/>
/// — the request's full decision log, oldest first.
///
/// For an automatic (access-rule) decision <see cref="Id"/>/<see cref="Name"/>/<see cref="Email"/> are null; for a
/// human decision they carry the approver (name/email denormalized by the server, null only when the user could not be
/// resolved).
/// </summary>
public class AccessRequestDecisionResponseModel
{
    /// <summary><c>human | automatic</c>.</summary>
    public string DeciderKind { get; init; } = AccessDeciderKindNames.Human;

    /// <summary>The human approver, or null for an automatic decision.</summary>
    public Guid? Id { get; init; }

    public string? Name { get; init; }

    public string? Email { get; init; }

    public string? Comment { get; init; }

    /// <summary>The verdict reached (<c>0 = deny, 1 = approve</c>).</summary>
    public AccessDecisionVerdict Verdict { get; init; }

    public DateTime DecidedAt { get; init; }
}
