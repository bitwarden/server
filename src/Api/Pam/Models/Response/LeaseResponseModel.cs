using Bit.Core.Models.Api;
using Bit.Core.Pam.Entities;

namespace Bit.Api.Pam.Models.Response;

public class LeaseResponseModel : ResponseModel
{
    public LeaseResponseModel(Lease lease)
        : base("lease")
    {
        ArgumentNullException.ThrowIfNull(lease);

        Id = lease.Id;
        CipherId = lease.CipherId;
        CollectionId = lease.CollectionId;
        OrganizationId = lease.OrganizationId;
        Status = lease.Status;
        NotBefore = lease.NotBefore;
        NotAfter = lease.NotAfter;
    }

    public Guid Id { get; }
    public Guid CipherId { get; }
    public Guid CollectionId { get; }
    public Guid OrganizationId { get; }
    public Core.Pam.Enums.LeaseStatus Status { get; }
    public DateTime NotBefore { get; }
    public DateTime NotAfter { get; }
}
