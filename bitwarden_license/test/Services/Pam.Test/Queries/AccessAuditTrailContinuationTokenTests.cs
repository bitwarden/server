using Bit.Pam.Models;
using Bit.Services.Pam.OrganizationFeatures.Queries;
using Xunit;

namespace Bit.Services.Pam.Test.Queries;

public class AccessAuditTrailContinuationTokenTests
{
    [Fact]
    public void From_ThenTryParse_RoundTripsTheExactPosition()
    {
        var row = new AccessAuditEvent
        {
            Id = Guid.NewGuid(),
            // Tick precision, as DATETIME2(7) stores.
            OccurredDate = new DateTime(638_600_123_456_789_012L, DateTimeKind.Utc),
        };

        Assert.True(AccessAuditTrailContinuationToken.TryParse(
            AccessAuditTrailContinuationToken.From(row), out var occurredAt, out var id));

        Assert.Equal(row.OccurredDate, occurredAt);
        Assert.Equal(DateTimeKind.Utc, occurredAt.Kind);
        Assert.Equal(row.Id, id);
    }

    [Fact]
    public void From_TwoRowsSharingAnInstant_ProducesDistinctTokens()
    {
        var occurredAt = new DateTime(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc);

        var first = AccessAuditTrailContinuationToken.From(new AccessAuditEvent
        {
            Id = Guid.NewGuid(),
            OccurredDate = occurredAt,
        });
        var second = AccessAuditTrailContinuationToken.From(new AccessAuditEvent
        {
            Id = Guid.NewGuid(),
            OccurredDate = occurredAt,
        });

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("")]
    [InlineData("_")]
    [InlineData("not-a-token")]
    [InlineData("638000000000000000")]
    [InlineData("638000000000000000_")]
    [InlineData("_0123456789abcdef0123456789abcdef")]
    [InlineData("638000000000000000_not-a-guid")]
    // Not the format From emits.
    [InlineData("638000000000000000_01234567-89ab-cdef-0123-456789abcdef")]
    [InlineData("-1_0123456789abcdef0123456789abcdef")]
    [InlineData("99999999999999999999_0123456789abcdef0123456789abcdef")]
    public void TryParse_AnythingItDidNotIssue_IsRefused(string token)
    {
        Assert.False(AccessAuditTrailContinuationToken.TryParse(token, out _, out _));
    }
}
