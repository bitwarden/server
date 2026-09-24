using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Models;

public class AccessRequestDetailsResponseModelTests
{
    /// <summary>
    /// A request whose lease was extended: the request's own window closed an hour ago, the lease it produced runs an
    /// hour longer. The details stamp both, and the response must carry them separately.
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
        // PAM-151: the client counts down from this, so the two ends must not collapse into one another.
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

        Assert.Equal(DateTimeKind.Utc, model.ProducedLeaseNotAfter!.Value.Kind);
        // Relabelled, not converted: the clock reading must be untouched.
        Assert.Equal(leaseEnd.TimeOfDay, model.ProducedLeaseNotAfter.Value.TimeOfDay);
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
