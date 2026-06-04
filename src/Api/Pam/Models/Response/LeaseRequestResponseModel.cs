using Bit.Core.Models.Api;
using Bit.Core.Pam.Entities;

namespace Bit.Api.Pam.Models.Response;

public class LeaseRequestResponseModel : ResponseModel
{
    public LeaseRequestResponseModel(LeaseRequest request)
        : base("leaseRequest")
    {
        ArgumentNullException.ThrowIfNull(request);

        Id = request.Id;
        CipherId = request.CipherId;
        CollectionId = request.CollectionId;
        OrganizationId = request.OrganizationId;
        Status = request.Status;
        NotBefore = request.NotBefore;
        NotAfter = request.NotAfter;
        Reason = request.Reason;
        CreationDate = request.CreationDate;
    }

    public Guid Id { get; }
    public Guid CipherId { get; }
    public Guid CollectionId { get; }
    public Guid OrganizationId { get; }
    public Core.Pam.Enums.LeaseRequestStatus Status { get; }
    public DateTime NotBefore { get; }
    public DateTime NotAfter { get; }
    public string? Reason { get; }
    public DateTime CreationDate { get; }
}
