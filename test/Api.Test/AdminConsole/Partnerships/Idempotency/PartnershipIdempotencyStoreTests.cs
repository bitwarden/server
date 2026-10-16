using System.Text;
using Bit.Api.AdminConsole.Partnerships.Idempotency;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Partnerships.Idempotency;

public class PartnershipIdempotencyStoreTests
{
    private const string Key = "key-1";
    private const string Hash = "hash-1";

    private readonly TestIdempotencyStore _test = new();
    private readonly Guid _partnershipId = Guid.NewGuid();
    private readonly PartnershipStoredResponse _response =
        new(201, "application/json", Encoding.UTF8.GetBytes("""{"externalId":"ext-secret-123"}"""));

    private PartnershipIdempotencyStore Store => _test.Store;

    [Fact]
    public async Task FirstClaim_Acquires()
    {
        var claim = await Store.ClaimAsync(_partnershipId, Key, Hash);

        Assert.Equal(PartnershipIdempotencyOutcome.Acquired, claim.Outcome);
    }

    [Fact]
    public async Task ClaimWhileInFlight_ReturnsInProgress()
    {
        await Store.ClaimAsync(_partnershipId, Key, Hash);

        var claim = await Store.ClaimAsync(_partnershipId, Key, Hash);

        Assert.Equal(PartnershipIdempotencyOutcome.InProgress, claim.Outcome);
    }

    [Fact]
    public async Task ConcurrentClaims_OnlyOneAcquires()
    {
        var claims = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => Store.ClaimAsync(_partnershipId, Key, Hash))));

        Assert.Single(claims, c => c.Outcome == PartnershipIdempotencyOutcome.Acquired);
        Assert.All(claims.Where(c => c.Outcome != PartnershipIdempotencyOutcome.Acquired),
            c => Assert.Equal(PartnershipIdempotencyOutcome.InProgress, c.Outcome));
    }

    [Fact]
    public async Task ClaimAfterInFlightLifetime_Acquires()
    {
        await Store.ClaimAsync(_partnershipId, Key, Hash);
        _test.Clock.Advance(PartnershipIdempotencyStore.InFlightLifetime + TimeSpan.FromSeconds(1));

        var claim = await Store.ClaimAsync(_partnershipId, Key, Hash);

        Assert.Equal(PartnershipIdempotencyOutcome.Acquired, claim.Outcome);
    }

    [Fact]
    public async Task ClaimAfterComplete_SameHash_ReplaysStoredResponse()
    {
        await Store.ClaimAsync(_partnershipId, Key, Hash);
        await Store.CompleteAsync(_partnershipId, Key, Hash, _response);

        var claim = await Store.ClaimAsync(_partnershipId, Key, Hash);

        Assert.Equal(PartnershipIdempotencyOutcome.Replay, claim.Outcome);
        Assert.Equal(_response.StatusCode, claim.Response!.StatusCode);
        Assert.Equal(_response.ContentType, claim.Response.ContentType);
        Assert.Equal(_response.Body, claim.Response.Body);
    }

    [Fact]
    public async Task ClaimAfterComplete_DifferentHash_ReturnsKeyReused()
    {
        await Store.ClaimAsync(_partnershipId, Key, Hash);
        await Store.CompleteAsync(_partnershipId, Key, Hash, _response);

        var claim = await Store.ClaimAsync(_partnershipId, Key, "hash-2");

        Assert.Equal(PartnershipIdempotencyOutcome.KeyReused, claim.Outcome);
        Assert.Null(claim.Response);
    }

    [Fact]
    public async Task SameKeyOnAnotherPartnership_Acquires()
    {
        await Store.ClaimAsync(_partnershipId, Key, Hash);
        await Store.CompleteAsync(_partnershipId, Key, Hash, _response);

        var claim = await Store.ClaimAsync(Guid.NewGuid(), Key, Hash);

        Assert.Equal(PartnershipIdempotencyOutcome.Acquired, claim.Outcome);
    }

    [Fact]
    public async Task ClaimAfterRelease_Acquires()
    {
        await Store.ClaimAsync(_partnershipId, Key, Hash);
        await Store.ReleaseAsync(_partnershipId, Key);

        var claim = await Store.ClaimAsync(_partnershipId, Key, Hash);

        Assert.Equal(PartnershipIdempotencyOutcome.Acquired, claim.Outcome);
    }

    [Fact]
    public async Task StoredResponse_ReplaysUntilResponseLifetimeThenExpires()
    {
        await Store.ClaimAsync(_partnershipId, Key, Hash);
        await Store.CompleteAsync(_partnershipId, Key, Hash, _response);

        _test.Clock.Advance(PartnershipIdempotencyStore.ResponseLifetime - TimeSpan.FromMinutes(1));
        var beforeExpiry = await Store.ClaimAsync(_partnershipId, Key, Hash);
        _test.Clock.Advance(TimeSpan.FromMinutes(2));
        var afterExpiry = await Store.ClaimAsync(_partnershipId, Key, Hash);

        Assert.Equal(PartnershipIdempotencyOutcome.Replay, beforeExpiry.Outcome);
        Assert.Equal(PartnershipIdempotencyOutcome.Acquired, afterExpiry.Outcome);
    }

    [Fact]
    public async Task Complete_DoesNotWriteBodyOrKeyInPlaintextToTheDistributedCache()
    {
        const string idempotencyKey = "partner-supplied-key-abc";
        await Store.ClaimAsync(_partnershipId, idempotencyKey, Hash);
        await Store.CompleteAsync(_partnershipId, idempotencyKey, Hash, _response);

        var written = _test.DistributedCache.Written.ToList();
        Assert.NotEmpty(written);
        Assert.All(written, entry =>
        {
            var value = Encoding.UTF8.GetString(entry.Value);
            Assert.DoesNotContain("ext-secret-123", value);
            Assert.DoesNotContain(Convert.ToBase64String(_response.Body), value);
            Assert.DoesNotContain(idempotencyKey, value);
            Assert.DoesNotContain(idempotencyKey, entry.Key);
        });
    }
}
