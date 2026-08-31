using Bit.Pam.Models;
using Bit.Services.Pam.OrganizationFeatures.Queries;
using Xunit;

namespace Bit.Services.Pam.Test.Queries;

/// <summary>
/// The trail's page position. It carries the last row's id alongside its instant because the instant alone does not
/// identify a row here: an action writes its before/after halves at one instant, so events sharing a timestamp are
/// ordinary in this store rather than a remote tie.
/// </summary>
public class AccessAuditTrailContinuationTokenTests
{
    [Fact]
    public void From_ThenTryParse_RoundTripsTheExactPosition()
    {
        var row = new AccessAuditEvent
        {
            Id = Guid.NewGuid(),
            // A tick-precision instant, which is what DATETIME2(7) stores and what the token must not round.
            OccurredAt = new DateTime(638_600_123_456_789_012L, DateTimeKind.Utc),
        };

        Assert.True(AccessAuditTrailContinuationToken.TryParse(
            AccessAuditTrailContinuationToken.From(row), out var occurredAt, out var id));

        Assert.Equal(row.OccurredAt, occurredAt);
        Assert.Equal(DateTimeKind.Utc, occurredAt.Kind);
        Assert.Equal(row.Id, id);
    }

    // Two rows recorded at the same instant produce different tokens, which is the whole reason the id is on there.
    [Fact]
    public void From_TwoRowsSharingAnInstant_ProducesDistinctTokens()
    {
        var occurredAt = new DateTime(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc);

        var first = AccessAuditTrailContinuationToken.From(new AccessAuditEvent
        {
            Id = Guid.NewGuid(),
            OccurredAt = occurredAt,
        });
        var second = AccessAuditTrailContinuationToken.From(new AccessAuditEvent
        {
            Id = Guid.NewGuid(),
            OccurredAt = occurredAt,
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
    // The dashed Guid form is not what From emits, so it is not one of ours either.
    [InlineData("638000000000000000_01234567-89ab-cdef-0123-456789abcdef")]
    [InlineData("-1_0123456789abcdef0123456789abcdef")]
    [InlineData("99999999999999999999_0123456789abcdef0123456789abcdef")]
    public void TryParse_AnythingItDidNotIssue_IsRefused(string token)
    {
        Assert.False(AccessAuditTrailContinuationToken.TryParse(token, out _, out _));
    }
}
