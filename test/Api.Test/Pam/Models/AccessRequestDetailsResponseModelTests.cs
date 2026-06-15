using Bit.Api.Pam.Models.Response;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;
using Xunit;

namespace Bit.Api.Test.Pam.Models;

public class AccessRequestDetailsResponseModelTests
{
    [Fact]
    public void Ctor_MarksTimestampsAsUtc()
    {
        // Regression guard: the approver inbox drops a request whose requested window has lapsed. When the stored
        // UTC instants are serialised without a 'Z' (Kind=Unspecified), a client east of UTC reparses them as local
        // time and the shift hides still-valid requests. The model must relabel the kind as UTC.
        var unspecified = new DateTime(2026, 6, 15, 13, 0, 0, DateTimeKind.Unspecified);
        var details = new AccessRequestDetails
        {
            Status = AccessRequestStatus.Pending,
            NotBefore = unspecified,
            NotAfter = unspecified.AddHours(1),
            CreationDate = unspecified.AddMinutes(-5),
            ResolvedDate = unspecified.AddMinutes(10),
        };

        var model = new AccessRequestDetailsResponseModel(details);

        Assert.Equal(DateTimeKind.Utc, model.RequestedNotBefore.Kind);
        Assert.Equal(DateTimeKind.Utc, model.RequestedNotAfter.Kind);
        Assert.Equal(DateTimeKind.Utc, model.SubmittedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, model.ResolvedAt!.Value.Kind);
        // SpecifyKind relabels without shifting the wall clock.
        Assert.Equal(unspecified.Ticks, model.RequestedNotBefore.Ticks);
    }

    [Fact]
    public void Ctor_LeavesNullResolvedDateNull()
    {
        var unspecified = new DateTime(2026, 6, 15, 13, 0, 0, DateTimeKind.Unspecified);
        var details = new AccessRequestDetails
        {
            Status = AccessRequestStatus.Pending,
            NotBefore = unspecified,
            NotAfter = unspecified.AddHours(1),
            CreationDate = unspecified,
            ResolvedDate = null,
        };

        var model = new AccessRequestDetailsResponseModel(details);

        Assert.Null(model.ResolvedAt);
    }
}
