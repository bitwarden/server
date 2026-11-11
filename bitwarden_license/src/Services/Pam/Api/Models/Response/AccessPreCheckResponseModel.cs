using Bit.HttpExtensions;
using Bit.Services.Pam.Enums;
using Bit.Services.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// The approval outcome a request for this cipher would get, so the client can offer the right form before
/// submitting.
/// </summary>
public class AccessPreCheckResponseModel : ResponseModel
{
    public AccessPreCheckResponseModel()
        : base("accessPreCheck")
    {
    }

    public AccessPreCheckResponseModel(Guid cipherId, AccessPreCheckResult result)
        : base("accessPreCheck")
    {
        ArgumentNullException.ThrowIfNull(result);

        CipherId = cipherId;
        ApprovalMode = result.ApprovalMode;
        HasActiveLease = result.HasActiveLease;
        DefaultDurationSeconds = result.DefaultDurationSeconds;
        MaxDurationSeconds = result.MaxDurationSeconds;
        CanStartLease = result.CanStartLease;
        SlotFreesAt = result.SlotFreesAt.AsUtc();
    }

    public Guid CipherId { get; set; }

    /// <summary>
    /// <see cref="AccessApprovalMode.Automatic"/> when a request would be approved immediately,
    /// <see cref="AccessApprovalMode.Human"/> when it needs an approver.
    /// </summary>
    public AccessApprovalMode ApprovalMode { get; set; }

    /// <summary>True when the caller already holds an active lease, so no request is needed.</summary>
    public bool HasActiveLease { get; set; }

    /// <summary>
    /// The duration to pre-select, from the rule's default or the global one, clamped to
    /// <see cref="MaxDurationSeconds"/>.
    /// </summary>
    public int DefaultDurationSeconds { get; set; }

    /// <summary>
    /// The longest duration (automatic path) or window (human path) a request may ask for, which is the rule's cap
    /// narrowed by the global ceiling. Submit enforces the same limit.
    /// </summary>
    public int MaxDurationSeconds { get; set; }

    /// <summary>
    /// False only when the single-active-lease constraint binds and another member holds the slot. A hint, re-checked
    /// at start; absent means true.
    /// </summary>
    public bool CanStartLease { get; set; } = true;

    /// <summary>When the lease holding the slot ends. Null when <see cref="CanStartLease"/> is true.</summary>
    public DateTime? SlotFreesAt { get; set; }
}
