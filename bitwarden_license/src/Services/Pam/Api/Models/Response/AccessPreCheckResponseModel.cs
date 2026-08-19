using Bit.HttpExtensions;
using Bit.Services.Pam.Enums;
using Bit.Services.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// The resolved approval outcome for a cipher, without submitting a request — lets the client present the right
/// workflow (pick a duration vs. pick a window and justify) before the requester commits.
/// </summary>
public class AccessPreCheckResponseModel : ResponseModel
{
    public AccessPreCheckResponseModel()
        : base("accessPreCheck")
    {
    }

    /// <param name="cipherId">
    /// The cipher the pre-check was run for. Passed in because <see cref="AccessPreCheckResult"/> describes only the
    /// outcome and does not carry the subject cipher.
    /// </param>
    /// <param name="result">The resolved approval outcome.</param>
    public AccessPreCheckResponseModel(Guid cipherId, AccessPreCheckResult result)
        : base("accessPreCheck")
    {
        ArgumentNullException.ThrowIfNull(result);

        CipherId = cipherId;
        ApprovalMode = result.ApprovalMode;
        HasActiveLease = result.HasActiveLease;
        DefaultDurationSeconds = result.DefaultDurationSeconds;
        MaxDurationSeconds = result.MaxDurationSeconds;
    }

    public Guid CipherId { get; set; }

    /// <summary>
    /// <see cref="AccessApprovalMode.Automatic"/> when a request would be approved immediately,
    /// <see cref="AccessApprovalMode.Human"/> when it needs an approver.
    /// </summary>
    public AccessApprovalMode ApprovalMode { get; set; }

    /// <summary>
    /// True when the caller already holds an active lease: reveal the credential, no request needed.
    /// </summary>
    public bool HasActiveLease { get; set; }

    /// <summary>
    /// The duration, in seconds, the request form should pre-select — the governing rule's default when it sets one,
    /// otherwise the global default, clamped to <see cref="MaxDurationSeconds"/>.
    /// </summary>
    public int DefaultDurationSeconds { get; set; }

    /// <summary>
    /// The longest duration (automatic path) or window span (human path), in seconds, that a request for this cipher
    /// may ask for: the governing rule's cap narrowed by the global ceiling. Clients should offer nothing above it —
    /// submit enforces the same number.
    /// </summary>
    public int MaxDurationSeconds { get; set; }
}
