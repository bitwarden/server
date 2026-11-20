using System.Security.Claims;
using Bit.Identity.IdentityServer;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.IdentityServer;

public class AuthorizationCodeStoreTests
{
    private const string Handle = "test-handle";
    private const string SubjectId = "test-subject-id";

    private readonly IHandleGenerationService _handleGenerationService;
    private readonly IPersistentGrantSerializer _serializer;

    public AuthorizationCodeStoreTests()
    {
        _handleGenerationService = Substitute.For<IHandleGenerationService>();
        _handleGenerationService.GenerateAsync(Arg.Any<int>()).Returns(Handle);

        _serializer = new PersistentGrantSerializer(new PersistentGrantOptions(),
            new EphemeralDataProtectionProvider());
    }

    [Fact]
    public async Task StoreAuthorizationCodeAsync_PersistsGrantWithCodeMetadata()
    {
        // Arrange
        var persistedGrantStore = Substitute.For<IPersistedGrantStore>();
        PersistedGrant storedGrant = null;
        await persistedGrantStore.StoreAsync(Arg.Do<PersistedGrant>(g => storedGrant = g));
        var sut = CreateSut(persistedGrantStore);
        var code = CreateAuthorizationCode();

        // Act
        var result = await sut.StoreAuthorizationCodeAsync(code);

        // Assert
        Assert.StartsWith(Handle, result);
        Assert.NotNull(storedGrant);
        Assert.Equal(IdentityServerConstants.PersistedGrantTypes.AuthorizationCode, storedGrant.Type);
        Assert.Equal(code.ClientId, storedGrant.ClientId);
        Assert.Equal(SubjectId, storedGrant.SubjectId);
        Assert.Equal(code.SessionId, storedGrant.SessionId);
        Assert.Equal(code.Description, storedGrant.Description);
        Assert.Equal(code.CreationTime, storedGrant.CreationTime);
        Assert.Equal(code.CreationTime.AddSeconds(code.Lifetime), storedGrant.Expiration);
        Assert.False(string.IsNullOrEmpty(storedGrant.Data));
    }

    [Fact]
    public async Task StoreAuthorizationCodeAsync_DoesNotPersistRawHandleAsKey()
    {
        // Arrange
        var persistedGrantStore = Substitute.For<IPersistedGrantStore>();
        PersistedGrant storedGrant = null;
        await persistedGrantStore.StoreAsync(Arg.Do<PersistedGrant>(g => storedGrant = g));
        var sut = CreateSut(persistedGrantStore);

        // Act
        await sut.StoreAuthorizationCodeAsync(CreateAuthorizationCode());

        // Assert
        Assert.NotNull(storedGrant);
        Assert.False(string.IsNullOrEmpty(storedGrant.Key));
        Assert.NotEqual(Handle, storedGrant.Key);
        Assert.DoesNotContain(Handle, storedGrant.Key);
    }

    [Fact]
    public async Task GetAuthorizationCodeAsync_AfterStore_ReturnsStoredCode()
    {
        // Arrange
        var sut = CreateSut(new InMemoryPersistedGrantStore());
        var code = CreateAuthorizationCode();
        var handle = await sut.StoreAuthorizationCodeAsync(code);

        // Act
        var result = await sut.GetAuthorizationCodeAsync(handle);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(code.ClientId, result.ClientId);
        Assert.Equal(code.SessionId, result.SessionId);
        Assert.Equal(code.RedirectUri, result.RedirectUri);
        Assert.Equal(code.RequestedScopes, result.RequestedScopes);
        Assert.Equal(code.CodeChallenge, result.CodeChallenge);
        Assert.Equal(code.CodeChallengeMethod, result.CodeChallengeMethod);
        Assert.Equal(code.Lifetime, result.Lifetime);
        Assert.Equal(SubjectId, result.Subject.FindFirst(JwtClaimTypes.Subject)?.Value);
    }

    [Fact]
    public async Task GetAuthorizationCodeAsync_UnknownCode_ReturnsNull()
    {
        // Arrange
        var sut = CreateSut(new InMemoryPersistedGrantStore());

        // Act
        var result = await sut.GetAuthorizationCodeAsync("unknown-code");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAuthorizationCodeAsync_GrantOfDifferentType_ReturnsNull()
    {
        // Arrange
        var persistedGrantStore = Substitute.For<IPersistedGrantStore>();
        PersistedGrant storedGrant = null;
        await persistedGrantStore.StoreAsync(Arg.Do<PersistedGrant>(g => storedGrant = g));
        var sut = CreateSut(persistedGrantStore);
        var handle = await sut.StoreAuthorizationCodeAsync(CreateAuthorizationCode());
        persistedGrantStore.GetAsync(storedGrant.Key).Returns(storedGrant);
        Assert.NotNull(await sut.GetAuthorizationCodeAsync(handle)); // Lookup reaches the grant.

        storedGrant.Type = IdentityServerConstants.PersistedGrantTypes.RefreshToken;

        // Act
        var result = await sut.GetAuthorizationCodeAsync(handle);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveAuthorizationCodeAsync_DoesNotRemoveGrant()
    {
        // Arrange
        var sut = CreateSut(new InMemoryPersistedGrantStore());
        var handle = await sut.StoreAuthorizationCodeAsync(CreateAuthorizationCode());

        // Act
        await sut.RemoveAuthorizationCodeAsync(handle);

        // Assert
        Assert.NotNull(await sut.GetAuthorizationCodeAsync(handle));
    }

    [Fact]
    public async Task RemoveAuthorizationCodeAsync_DoesNotCallPersistedGrantStore()
    {
        // Arrange
        var persistedGrantStore = Substitute.For<IPersistedGrantStore>();
        var sut = CreateSut(persistedGrantStore);

        // Act
        await sut.RemoveAuthorizationCodeAsync(Handle);

        // Assert
        await persistedGrantStore.DidNotReceiveWithAnyArgs().RemoveAsync(default);
        await persistedGrantStore.DidNotReceiveWithAnyArgs().RemoveAllAsync(default);
    }

    private AuthorizationCodeStore CreateSut(IPersistedGrantStore persistedGrantStore)
    {
        return new AuthorizationCodeStore(persistedGrantStore, _serializer, _handleGenerationService,
            NullLogger<DefaultAuthorizationCodeStore>.Instance);
    }

    private static AuthorizationCode CreateAuthorizationCode()
    {
        return new AuthorizationCode
        {
            ClientId = "web",
            Subject = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(JwtClaimTypes.Subject, SubjectId)], "test")),
            SessionId = "test-session-id",
            Description = "test-description",
            CreationTime = DateTime.UtcNow,
            Lifetime = (int)TimeSpan.FromMinutes(5).TotalSeconds,
            RedirectUri = "https://localhost/sso-connector.html",
            RequestedScopes = ["api", "offline_access"],
            CodeChallenge = "test-code-challenge",
            CodeChallengeMethod = OidcConstants.CodeChallengeMethods.Sha256,
        };
    }
}
