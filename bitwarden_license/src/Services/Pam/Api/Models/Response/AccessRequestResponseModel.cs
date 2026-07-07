using Bit.HttpExtensions;
using Bit.Pam.Entities;

namespace Bit.Services.Pam.Api.Models.Response;

public class AccessRequestResponseModel : ResponseModel
{
    public AccessRequestResponseModel(AccessRequest request)
        : base("accessRequest")
    {
        ArgumentNullException.ThrowIfNull(request);

        Id = request.Id;
        CipherId = request.CipherId;
        CollectionId = request.CollectionId;
        OrganizationId = request.OrganizationId;
        Status = request.Status.ToApiStatus(hasLease: false);
        LeaseNotBefore = request.NotBefore.AsUtc();
        LeaseNotAfter = request.NotAfter.AsUtc();
        Reason = request.Reason;
        SubmittedAt = request.CreationDate.AsUtc();
    }

    public Guid Id { get; }
    public Guid CipherId { get; }
    public Guid CollectionId { get; }
    public Guid OrganizationId { get; }

    /// <summary>The request's lifecycle state.</summary>
    public AccessRequestStatus Status { get; }

    /// <summary>The activation window resolved at submit — when this request may be promoted to a lease.</summary>
    public DateTime LeaseNotBefore { get; }
    public DateTime LeaseNotAfter { get; }
    public string? Reason { get; }
    public DateTime SubmittedAt { get; }
}
