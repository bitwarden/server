using Bit.Api.Pam.Models.Response;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Pam.Models;

public class MemberLeaseResponseModelTests
{
    [Theory, BitAutoData]
    public void Ctor_MapsLeaseToClientShape(Lease lease)
    {
        lease.Status = LeaseStatus.Active;

        var model = new MemberLeaseResponseModel(lease);

        Assert.Equal(lease.Id, model.Id);
        Assert.Equal(lease.LeaseRequestId, model.RequestId);
        Assert.Equal(lease.CipherId, model.CipherId);
        Assert.Equal(lease.CollectionId, model.CollectionId);
        Assert.Equal(lease.OrganizationId, model.OrganizationId);
        Assert.Equal(lease.RequesterId, model.GranteeUserId);
        Assert.Equal(LeaseStatusName.Active, model.Status);
        Assert.Equal(lease.NotBefore, model.NotBefore);
        Assert.Equal(lease.NotAfter, model.NotAfter);
        Assert.Equal(lease.RevokedDate, model.RevokedAt);
        Assert.Equal(lease.RevokedBy, model.RevokedByUserId);
        Assert.Null(model.RuleId);
        Assert.Null(model.RevocationReason);
    }
}
