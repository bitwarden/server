using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Models;

public class AccessRequestDetailsResponseModelTests
{
    /// <summary>
    /// An extended lease: the request's window closed an hour ago, but the lease it produced ends an hour from now.
    /// </summary>
    private static AccessRequestDetails ExtendedLease(DateTime now)
    {
        var details = new AccessRequestDetails
        {
            Id = Guid.NewGuid(),
            NotBefore = now.AddHours(-3),
            NotAfter = now.AddHours(-1),
        };
        details.StampDerivedStatuses(
            AccessRequestAction.Approved,
            (Guid.NewGuid(), AccessLeaseAction.None, now.AddHours(1)),
            now);
        return details;
    }

    [Fact]
    public void Constructor_CarriesTheProducedLeasesOwnEnd_NotTheRequestsWindow()
    {
        // The client counts down from this, so the two ends must not collapse into one another.
        var now = DateTime.UtcNow;
        var details = ExtendedLease(now);

        var model = new AccessRequestDetailsResponseModel(details);

        Assert.Equal(AccessLeaseStatus.Active, model.ProducedLeaseStatus);
        Assert.Equal(details.ProducedLeaseNotAfter!.Value, model.ProducedLeaseNotAfter!.Value);
        Assert.True(model.ProducedLeaseNotAfter > model.LeaseNotAfter);
    }

    [Fact]
    public void Constructor_MarksTheProducedLeaseEndAsUtcWithoutShiftingIt()
    {
        // Dapper materializes it as Kind.Unspecified; serialized as-is a browser would read it as local time.
        var leaseEnd = new DateTime(2026, 9, 18, 10, 52, 0, DateTimeKind.Unspecified);
        var details = new AccessRequestDetails { Id = Guid.NewGuid(), ProducedLeaseNotAfter = leaseEnd };

        var model = new AccessRequestDetailsResponseModel(details);

        Assert.NotNull(model.ProducedLeaseNotAfter);
        var producedLeaseNotAfter = model.ProducedLeaseNotAfter.Value;
        Assert.Equal(DateTimeKind.Utc, producedLeaseNotAfter.Kind);
        // Relabelled, not shifted.
        Assert.Equal(leaseEnd.TimeOfDay, producedLeaseNotAfter.TimeOfDay);
    }

    [Fact]
    public void StampDerivedStatuses_LeavesTheProducedLeaseEndNullWithoutALease()
    {
        var now = DateTime.UtcNow;
        var details = new AccessRequestDetails { Id = Guid.NewGuid(), NotAfter = now.AddHours(1) };

        details.StampDerivedStatuses(AccessRequestAction.Approved, producedLease: null, now);

        Assert.Null(new AccessRequestDetailsResponseModel(details).ProducedLeaseNotAfter);
    }
}
