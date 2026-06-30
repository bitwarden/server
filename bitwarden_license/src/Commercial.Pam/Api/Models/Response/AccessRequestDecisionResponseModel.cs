namespace Bit.Commercial.Pam.Api.Models.Response;

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
    public string DeciderKind { get; set; } = "human";

    /// <summary>The human approver's user id, or null for an automatic decision.</summary>
    public Guid? Id { get; set; }

    /// <summary>The human approver's display name; null for an automatic decision, or when the user could not be resolved.</summary>
    public string? Name { get; set; }

    /// <summary>The human approver's email; null for an automatic decision, or when the user could not be resolved.</summary>
    public string? Email { get; set; }

    /// <summary>The optional note the approver left with the decision.</summary>
    public string? Comment { get; set; }

    /// <summary>The verdict reached (<c>0 = deny, 1 = approve</c>).</summary>
    public AccessDecisionVerdict Verdict { get; set; }

    /// <summary>When the decision was recorded (UTC).</summary>
    public DateTime DecidedAt { get; set; }
}
