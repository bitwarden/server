using Bit.HttpExtensions;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// An access request as its requester sees it right after submit — the shape <see cref="AccessRequestResultResponseModel"/>
/// wraps for the cipher-lease submit response.
/// </summary>
public class AccessRequestResponseModel : ResponseModel
{
    public AccessRequestResponseModel()
        : base("accessRequest")
    {
    }

    /// <summary>The access request's unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The cipher access was requested for.</summary>
    public Guid CipherId { get; set; }

    /// <summary>The collection the cipher belongs to, through which the request is governed.</summary>
    public Guid CollectionId { get; set; }

    /// <summary>The organization that owns the cipher.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The request's lifecycle state.</summary>
    public AccessRequestStatus Status { get; set; }

    /// <summary>The activation window resolved at submit — when this request may be promoted to a lease.</summary>
    public DateTime LeaseNotBefore { get; set; }

    /// <summary>The end of the resolved activation window (UTC); see <see cref="LeaseNotBefore"/>.</summary>
    public DateTime LeaseNotAfter { get; set; }

    /// <summary>The optional justification the requester supplied when opening the request.</summary>
    public string? Reason { get; set; }

    /// <summary>When the request was opened (UTC).</summary>
    public DateTime SubmittedAt { get; set; }
}
