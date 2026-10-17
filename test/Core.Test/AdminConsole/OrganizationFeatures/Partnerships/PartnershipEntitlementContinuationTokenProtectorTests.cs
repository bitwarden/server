using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Partnerships;

public class PartnershipEntitlementContinuationTokenProtectorTests
{
    private static readonly PartnershipEntitlementKeyset _keyset =
        new(new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc).AddTicks(1234567), Guid.NewGuid());

    private readonly PartnershipEntitlementContinuationTokenProtector _sut =
        new(new EphemeralDataProtectionProvider());

    [Fact]
    public void TryUnprotect_SamePartnershipAndFilters_ReturnsKeysetToTheTick()
    {
        var request = NewRequest(PartnershipEntitlementState.Active, DateTime.UtcNow);
        var token = _sut.Protect(request, _keyset);

        Assert.True(_sut.TryUnprotect(request with { ContinuationToken = token }, out var after));
        Assert.Equal(_keyset.RevisionDate.Ticks, after.RevisionDate.Ticks);
        Assert.Equal(_keyset.Id, after.Id);
    }

    [Fact]
    public void TryUnprotect_NoFilters_RoundTrips()
    {
        var request = NewRequest(null, null);
        var token = _sut.Protect(request, _keyset);

        Assert.True(_sut.TryUnprotect(request with { ContinuationToken = token }, out var after));
        Assert.Equal(_keyset, after);
    }

    [Fact]
    public void Protect_TokenIsUrlSafe()
    {
        var token = _sut.Protect(NewRequest(null, null), _keyset);

        Assert.Matches("^[A-Za-z0-9_-]+$", token);
    }

    [Fact]
    public void TryUnprotect_OtherPartnership_Fails()
    {
        var request = NewRequest(null, null);
        var token = _sut.Protect(request, _keyset);

        Assert.False(_sut.TryUnprotect(
            request with { OrganizationPartnershipId = Guid.NewGuid(), ContinuationToken = token }, out _));
    }

    [Fact]
    public void TryUnprotect_DifferentState_Fails()
    {
        var request = NewRequest(PartnershipEntitlementState.Active, null);
        var token = _sut.Protect(request, _keyset);

        Assert.False(_sut.TryUnprotect(
            request with { State = PartnershipEntitlementState.Canceled, ContinuationToken = token }, out _));
    }

    [Fact]
    public void TryUnprotect_StateDropped_Fails()
    {
        var request = NewRequest(PartnershipEntitlementState.Active, null);
        var token = _sut.Protect(request, _keyset);

        Assert.False(_sut.TryUnprotect(request with { State = null, ContinuationToken = token }, out _));
    }

    [Fact]
    public void TryUnprotect_DifferentRevisedAfter_Fails()
    {
        var revisedAfter = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var request = NewRequest(null, revisedAfter);
        var token = _sut.Protect(request, _keyset);

        Assert.False(_sut.TryUnprotect(
            request with { RevisedAfter = revisedAfter.AddTicks(1), ContinuationToken = token }, out _));
    }

    [Fact]
    public void TryUnprotect_Tampered_Fails()
    {
        var request = NewRequest(null, null);
        var token = _sut.Protect(request, _keyset);
        var tampered = token[..^2] + (token[^2] == 'A' ? 'B' : 'A') + token[^1];

        Assert.False(_sut.TryUnprotect(request with { ContinuationToken = tampered }, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a token!")]
    [InlineData("AAAA")]
    public void TryUnprotect_Garbage_Fails(string token)
    {
        Assert.False(_sut.TryUnprotect(NewRequest(null, null) with { ContinuationToken = token }, out _));
    }

    [Fact]
    public void TryUnprotect_TokenFromAnotherKeyRing_Fails()
    {
        var request = NewRequest(null, null);
        var token = new PartnershipEntitlementContinuationTokenProtector(new EphemeralDataProtectionProvider())
            .Protect(request, _keyset);

        Assert.False(_sut.TryUnprotect(request with { ContinuationToken = token }, out _));
    }

    private static ListPartnershipEntitlementsRequest NewRequest(
        PartnershipEntitlementState? state, DateTime? revisedAfter) =>
        new() { OrganizationPartnershipId = Guid.NewGuid(), State = state, RevisedAfter = revisedAfter };
}
