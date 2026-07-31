using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Services.Pam.Api.Models.Response;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;
using ApiEnums = Bit.Services.Pam.Api.Models;

namespace Bit.Services.Pam.Test.Api.Models;

public class AccessLeaseResponseModelTests
{
    [Theory, BitAutoData]
    public void Ctor_MapsLeaseToClientShape(AccessLease lease)
    {
        lease.Status = AccessLeaseStatus.Active;

        var model = new AccessLeaseResponseModel(lease);

        Assert.Equal(lease.Id, model.Id);
        Assert.Equal(lease.AccessRequestId, model.RequestId);
        Assert.Equal(lease.CipherId, model.CipherId);
        Assert.Equal(lease.CollectionId, model.CollectionId);
        Assert.Equal(lease.OrganizationId, model.OrganizationId);
        Assert.Equal(lease.RequesterId, model.RequesterId);
        Assert.Equal(ApiEnums.AccessLeaseStatus.Active, model.Status);
        Assert.Equal(lease.NotBefore, model.NotBefore);
        Assert.Equal(lease.NotAfter, model.NotAfter);
        Assert.Equal(lease.RevokedDate, model.RevokedAt);
        Assert.Equal(lease.RevokedBy, model.RevokedByUserId);
        Assert.Null(model.RuleId);
        Assert.Null(model.RevocationReason);
    }

    [Fact]
    public void Ctor_MarksTimestampsAsUtc()
    {
        // Dapper materialises stored UTC instants with Kind=Unspecified; the model must relabel them UTC so the
        // serialised JSON carries a 'Z' and clients don't reparse them as local time.
        var unspecified = new DateTime(2026, 6, 15, 13, 0, 0, DateTimeKind.Unspecified);
        var lease = new AccessLease
        {
            Status = AccessLeaseStatus.Active,
            NotBefore = unspecified,
            NotAfter = unspecified.AddHours(1),
            RevokedDate = unspecified.AddMinutes(30),
        };

        var model = new AccessLeaseResponseModel(lease);

        Assert.Equal(DateTimeKind.Utc, model.NotBefore.Kind);
        Assert.Equal(DateTimeKind.Utc, model.NotAfter.Kind);
        Assert.Equal(DateTimeKind.Utc, model.RevokedAt!.Value.Kind);
        // SpecifyKind relabels without shifting the wall clock.
        Assert.Equal(unspecified.Ticks, model.NotBefore.Ticks);
    }

    [Fact]
    public void Ctor_LeavesNullRevokedDateNull()
    {
        var lease = new AccessLease
        {
            Status = AccessLeaseStatus.Active,
            NotBefore = new DateTime(2026, 6, 15, 13, 0, 0, DateTimeKind.Unspecified),
            NotAfter = new DateTime(2026, 6, 15, 14, 0, 0, DateTimeKind.Unspecified),
            RevokedDate = null,
        };

        var model = new AccessLeaseResponseModel(lease);

        Assert.Null(model.RevokedAt);
    }
}
