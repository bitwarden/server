using Bit.Core.Models.Api;
using Bit.Pam.Entities;

namespace Bit.Api.Pam.Models.Response;

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
        Status = AccessRequestStatusNames.From(request.Status, hasLease: false);
        NotBefore = request.NotBefore.AsUtc();
        NotAfter = request.NotAfter.AsUtc();
        Reason = request.Reason;
        CreationDate = request.CreationDate.AsUtc();
    }

    public Guid Id { get; }
    public Guid CipherId { get; }
    public Guid CollectionId { get; }
    public Guid OrganizationId { get; }

    /// <summary><c>pending | approved | activated | denied | cancelled | expired</c>.</summary>
    public string Status { get; }

    public DateTime NotBefore { get; }
    public DateTime NotAfter { get; }
    public string? Reason { get; }
    public DateTime CreationDate { get; }
}
