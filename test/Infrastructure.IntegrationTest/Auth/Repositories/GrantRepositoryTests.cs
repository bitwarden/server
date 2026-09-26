using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Auth.Repositories;

public class GrantRepositoryTests
{
    // -------------------------------------------------------------------------------------------
    // Test helpers
    // -------------------------------------------------------------------------------------------

    private const string RefreshTokenType = "refresh_token";

    /// <summary>
    /// Truncates a <see cref="DateTime"/> to second-level precision (preserving <see cref="DateTime.Kind"/>).
    /// Round-tripped values can lose sub-second precision on some providers (e.g. Dapper binds
    /// <c>DateTime</c> parameters as legacy <c>datetime</c> with ~3.33ms granularity), so all
    /// values written by these tests are truncated to whole seconds to keep equality asserts exact.
    /// </summary>
    private static DateTime TruncateToSecond(DateTime value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, value.Kind);

    private static Grant CreateTestGrant(
        string key = null,
        string subjectId = null,
        string clientId = "mobile",
        DateTime? creationDate = null,
        DateTime? expirationDate = null,
        string data = null)
    {
        return new Grant
        {
            Key = key ?? Guid.NewGuid().ToString(),
            Type = RefreshTokenType,
            SubjectId = subjectId ?? Guid.NewGuid().ToString(),
            ClientId = clientId,
            CreationDate = creationDate ?? TruncateToSecond(DateTime.UtcNow),
            ExpirationDate = expirationDate,
            Data = data ?? "{}",
        };
    }

    // -------------------------------------------------------------------------------------------
    // SaveAsync
    // -------------------------------------------------------------------------------------------

    [DatabaseTheory, DatabaseData]
    public async Task SaveAsync_InputIsNotTypeGrant_ThrowsArgumentException(
        IGrantRepository sutRepository)
    {
        // GrantItem is an IGrant (the Cosmos shape) but not the Grant the SQL repositories persist
        var invalidGrant = new GrantItem { Key = Guid.NewGuid().ToString() };

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => sutRepository.SaveAsync(invalidGrant));

        Assert.Equal("obj", exception.ParamName);
    }

    [DatabaseTheory, DatabaseData]
    public async Task SaveAsync_GrantIsNull_GrantCreated(
        IGrantRepository sutRepository)
    {
        var issuedAt = TruncateToSecond(DateTime.UtcNow);
        var grant = CreateTestGrant(
            creationDate: issuedAt,
            expirationDate: issuedAt.AddDays(30));

        await sutRepository.SaveAsync(grant);

        var stored = await sutRepository.GetByKeyAsync(grant.Key);
        Assert.NotNull(stored);
        Assert.Equal(grant.Key, stored!.Key);
    }

    /// <summary>
    /// Saving a grant whose key already exists must update the stored row. This is the
    /// refresh-token slide: IdentityServer stores refresh tokens with
    /// <c>RefreshTokenUsage = ReUse</c>, so every refresh re-saves the grant under the same key
    /// with a reset creation time and a new expiration. A repository that drops that update
    /// freezes the row at issuance and forces clients into a full re-authentication once the
    /// original window lapses, no matter how active they are.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task SaveAsync_GrantIsNotNull_GrantExpiryUpdated(
        IGrantRepository sutRepository)
    {
        // Arrange — grant issued with a 30-day window
        var issuedAt = TruncateToSecond(DateTime.UtcNow);
        var subjectId = Guid.NewGuid().ToString();
        var grant = CreateTestGrant(
            subjectId: subjectId,
            creationDate: issuedAt,
            expirationDate: issuedAt.AddDays(30));
        await sutRepository.SaveAsync(grant);

        // Act — 29 days later the client refreshes: same key, slid window, re-serialized data
        var refreshedAt = issuedAt.AddDays(29);
        await sutRepository.SaveAsync(CreateTestGrant(
            key: grant.Key,
            subjectId: subjectId,
            creationDate: refreshedAt,
            expirationDate: refreshedAt.AddDays(30),
            data: "{\"CreationTime\":\"refreshed\"}"));

        // Assert — the stored row carries the slid dates and data, not the issuance values
        var stored = await sutRepository.GetByKeyAsync(grant.Key);
        Assert.NotNull(stored);
        Assert.Equal(refreshedAt, stored!.CreationDate);
        Assert.Equal(refreshedAt.AddDays(30), stored.ExpirationDate);
        Assert.Equal("{\"CreationTime\":\"refreshed\"}", stored.Data);
    }
}
