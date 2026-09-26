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
    private const string AuthorizationCodeType = "authorization_code";

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
        string sessionId = null,
        string clientId = "mobile",
        string type = RefreshTokenType,
        DateTime? creationDate = null,
        DateTime? expirationDate = null,
        string data = null)
    {
        return new Grant
        {
            Key = key ?? Guid.NewGuid().ToString(),
            Type = type,
            SubjectId = subjectId ?? Guid.NewGuid().ToString(),
            SessionId = sessionId,
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

    // -------------------------------------------------------------------------------------------
    // GetManyAsync
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Non-null filters apply on every column and the results come back as an
    /// <c>ICollection&lt;IGrant&gt;</c>: two matching rows are returned, distractors differing in
    /// client, subject, or type are excluded, and a query with no matches returns an empty
    /// collection rather than throwing.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetManyAsync_WithNonNullFilters_ReturnsOnlyMatchingGrants(
        IGrantRepository sutRepository)
    {
        // Arrange — two matching grants plus distractors differing in client, subject, and type
        var subjectId = Guid.NewGuid().ToString();
        var sessionId = Guid.NewGuid().ToString();
        await sutRepository.SaveAsync(CreateTestGrant(subjectId: subjectId, sessionId: sessionId, clientId: "mobile"));
        await sutRepository.SaveAsync(CreateTestGrant(subjectId: subjectId, sessionId: sessionId, clientId: "mobile"));
        await sutRepository.SaveAsync(CreateTestGrant(subjectId: subjectId, sessionId: sessionId, clientId: "desktop"));
        await sutRepository.SaveAsync(CreateTestGrant(sessionId: sessionId, clientId: "mobile"));
        await sutRepository.SaveAsync(CreateTestGrant(
            subjectId: subjectId, sessionId: sessionId, clientId: "mobile", type: AuthorizationCodeType));

        // Act
        var grants = await sutRepository.GetManyAsync(subjectId, sessionId, "mobile", RefreshTokenType);

        // Assert — exactly the two matching rows
        Assert.Equal(2, grants.Count);
        Assert.All(grants, g => Assert.Equal(subjectId, g.SubjectId));
        Assert.All(grants, g => Assert.Equal("mobile", g.ClientId));
        Assert.All(grants, g => Assert.Equal(RefreshTokenType, g.Type));

        // A query matching nothing returns an empty collection
        Assert.Empty(await sutRepository.GetManyAsync(
            Guid.NewGuid().ToString(), sessionId, "mobile", RefreshTokenType));
    }

    /// <summary>
    /// A null filter argument means "do not filter on this field", matching the Grant_Read
    /// stored procedure's <c>@param IS NULL OR column = @param</c> wildcard pattern and the
    /// partial filters Duende's <c>PersistedGrantFilter</c> produces. The previous EF predicate
    /// translated nulls to "the column must be NULL", which matched nothing for Bitwarden data
    /// (SubjectId/ClientId/Type are always populated) — this covers the null-client and
    /// null-session wildcards in a single arrange/act/assert so a regression on either fails loudly.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetManyAsync_WithNullFilters_TreatsNullAsWildcard(
        IGrantRepository sutRepository)
    {
        // Arrange — the subject's grants span two clients and three session-id states
        // (one session, another session, none), plus another subject's grant
        var subjectId = Guid.NewGuid().ToString();
        var sessionId = Guid.NewGuid().ToString();
        await sutRepository.SaveAsync(CreateTestGrant(subjectId: subjectId, sessionId: sessionId, clientId: "mobile"));
        await sutRepository.SaveAsync(CreateTestGrant(
            subjectId: subjectId, sessionId: Guid.NewGuid().ToString(), clientId: "mobile"));
        await sutRepository.SaveAsync(CreateTestGrant(subjectId: subjectId, clientId: "desktop"));
        await sutRepository.SaveAsync(CreateTestGrant(sessionId: sessionId, clientId: "mobile"));

        // Act — no client or session filter
        var grants = await sutRepository.GetManyAsync(subjectId, null, null, RefreshTokenType);

        // Assert — all three of the subject's grants return regardless of client or session id
        Assert.Equal(3, grants.Count);
        Assert.All(grants, g => Assert.Equal(subjectId, g.SubjectId));
    }

    // -------------------------------------------------------------------------------------------
    // DeleteManyAsync
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Null filters are wildcards and non-null filters still apply, matching the Grant_Delete
    /// stored procedure: revoking a subject's refresh tokens with no client filter deletes them
    /// across all clients, while the subject's other grant types and other subjects' grants survive.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task DeleteManyAsync_WithNullClientFilter_DeletesAcrossClientsButOnlyMatchingTypeAndSubject(
        IGrantRepository sutRepository)
    {
        // Arrange — the subject's refresh tokens on two clients, an authorization code, and
        // another subject's grant
        var subjectId = Guid.NewGuid().ToString();
        var sessionId = Guid.NewGuid().ToString();
        var mobileGrant = CreateTestGrant(subjectId: subjectId, sessionId: sessionId, clientId: "mobile");
        var desktopGrant = CreateTestGrant(subjectId: subjectId, sessionId: sessionId, clientId: "desktop");
        var authorizationCode = CreateTestGrant(
            subjectId: subjectId, sessionId: sessionId, type: AuthorizationCodeType);
        var otherSubjectGrant = CreateTestGrant(sessionId: sessionId, clientId: "mobile");
        await sutRepository.SaveAsync(mobileGrant);
        await sutRepository.SaveAsync(desktopGrant);
        await sutRepository.SaveAsync(authorizationCode);
        await sutRepository.SaveAsync(otherSubjectGrant);

        // Act — revoke the subject's refresh tokens, no client filter
        await sutRepository.DeleteManyAsync(subjectId, null, null, RefreshTokenType);

        // Assert — both refresh tokens deleted across clients; the authorization code and the
        // other subject's grant survive
        Assert.Null(await sutRepository.GetByKeyAsync(mobileGrant.Key));
        Assert.Null(await sutRepository.GetByKeyAsync(desktopGrant.Key));
        Assert.NotNull(await sutRepository.GetByKeyAsync(authorizationCode.Key));
        Assert.NotNull(await sutRepository.GetByKeyAsync(otherSubjectGrant.Key));
    }
}
