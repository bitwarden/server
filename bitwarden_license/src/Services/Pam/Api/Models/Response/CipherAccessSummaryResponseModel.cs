using Bit.HttpExtensions;
using Bit.Services.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>The facts behind one gated cipher's badge, from the batch access-state read.</summary>
public class CipherAccessSummaryResponseModel : ResponseModel
{
    public CipherAccessSummaryResponseModel()
        : base("cipherAccessSummary")
    {
    }

    public CipherAccessSummaryResponseModel(CipherAccessSummary summary)
        : base("cipherAccessSummary")
    {
        ArgumentNullException.ThrowIfNull(summary);

        CipherId = summary.CipherId;
        ActiveLeaseNotAfter = summary.ActiveLeaseNotAfter.AsUtc();
        HasApprovedRequest = summary.HasApprovedRequest;
        HasPendingRequest = summary.HasPendingRequest;
        CanRequest = summary.CanRequest;
    }

    public Guid CipherId { get; set; }

    /// <summary>When the caller's active lease ends; null without one.</summary>
    public DateTime? ActiveLeaseNotAfter { get; set; }

    /// <summary>An approved request awaits the caller's activation, with a window that can still produce access.</summary>
    public bool HasApprovedRequest { get; set; }

    /// <summary>A request awaits a decision.</summary>
    public bool HasPendingRequest { get; set; }

    /// <summary>
    /// False when no lease or request is in play and the rule's conditions would refuse a new request now, as in
    /// <see cref="CipherAccessStateResponseModel.CanRequest"/>.
    /// </summary>
    public bool CanRequest { get; set; } = true;
}
