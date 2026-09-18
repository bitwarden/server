namespace Bit.Identity.IdentityServer;

public record ApiClientConfiguration
{
    public required string Id { get; init; }
    public int RefreshTokenSlidingDays { get; init; }
    public int AccessTokenLifetimeSeconds { get; init; }
    public string[]? Scopes { get; init; }
    public bool ApplyAbsoluteExpirationOnRefreshToken { get; init; }
    public int? SlidingRefreshTokenLifetimeSecondsOverride { get; init; }
    public int? AbsoluteRefreshTokenLifetimeSeconds { get; init; }
    public ICollection<string>? RedirectUris { get; init; }
    public ICollection<string>? PostLogoutRedirectUris { get; init; }
    public ICollection<string>? AllowedCorsOrigins { get; init; }
}
