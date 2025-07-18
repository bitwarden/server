# OtpTokenProvider

The `OtpTokenProvider` is a token provider service for generating and validating Time-Based one-time passwords (TOTP). It provides a secure way to create temporary tokens for various authentication and verification scenarios. The provider can be configured to generate tokens specific to your use case by using the options pattern in the DI pipeline.

## Overview

The OTP Token Provider generates secure, time-limited tokens that can be used for:

- Two-factor authentication
- Temporary access tokens for Sends
- Any scenario requiring short-lived verification codes

## Features

- **Configurable Token Length**: Default 6 characters, customizable
- **Character Set Options**: Numeric (default), alphabetic, or mixed
- **Distributed Caching**: Uses CosmosDb for cloud, or the configured database otherwise.
- **TTL Management**: Configurable expiration (default 5 minutes)
- **Secure Generation**: Uses cryptographically secure random generation
- **One-Time Use**: Tokens are automatically deleted from the cache after successful validation

## Architecture

### Interface: `IOtpTokenProvider<TOptions>`

```csharp
public interface IOtpTokenProvider<TOptions>
    where TOptions : DefaultOtpTokenProviderOptions
{
    Task<string?> GenerateTokenAsync(string tokenProviderName, string purpose, string uniqueIdentifier);
    Task<bool> ValidateTokenAsync(string token, string tokenProviderName, string purpose, string uniqueIdentifier);
}
```

### Implementation: `OtpTokenProvider`

The provider is initialized with:

- **Distributed Cache**: Storage backend for tokens (using "persistent" keyed service)
- **IOptions<TOptions>**: Configuration options for token generation and caching

## Usage

### Basic Setup

If your class needs the use the `IOtpTokenProvider` you can inject it like any other injectable class from the DI.

### Generating a Token

```csharp
// Generate a new OTP with token provider name, purpose and unique identifier
string token = await otpProvider.GenerateTokenAsync("EmailToken", "email_verification", $"{userId}_{securityStamp}");
// Returns: "123456" (6-digit numeric by default)
```

### Validating a Token

```csharp
// Validate user-provided token with same parameters used for generation
bool isValid = await otpProvider.ValidateTokenAsync("123456", "EmailToken", "email_verification", $"{userId}_{securityStamp}");
// Returns: true if valid, false otherwise
// Note: Valid tokens are automatically removed from cache
```

### Custom Configurations

If you need to modify the default options you can do so by creating an extension of the `DefaultOtpTokenProviderOptions` and using that class as the TOptions when injecting another IOtpTokenProvider service.

#### OtpTokenProviderOptions

```csharp
public class DefaultOtpTokenProviderOptions
{ ... }

public class UserEmailOtpTokenOptions : DefaultOtpTokenProviderOptions { }
```

#### Service Collection

```csharp
public static IdentityBuilder AddCustomIdentityServices(
    this IServiceCollection services, GlobalSettings globalSettings)
{
    // possible customization
    services.Configure<UserEmailOtpTokenOptions>(options =>
    {
        options.TokenLength = 8;
        // The other options are left default
    });

    // TryAddTransient open generics -> this allows us to inject IOtpTokenProvider<T> without having to specify the specific type here.
    services.TryAddTransient(typeof(IOtpTokenProvider<>), typeof(OtpTokenProvider<>);
}
```

#### Usage

```csharp
public class UserEmailTokenProvider(
    IOtpTokenProvider<UserEmailOtpTokenOptions> otpTokenProvider
)
{
    private readonly IOtpTokenProvider<UserEmailOtpTokenOptions> _otpTokenProvider = otpTokenProvider;
    ...
}
```

## Configuration Options

### Token Properties

| Property       | Default | Description                              |
| -------------- | ------- | ---------------------------------------- |
| `TokenLength`  | 6       | Number of characters in generated token  |
| `TokenAlpha`   | false   | Include alphabetic characters (a-z, A-Z) |
| `TokenNumeric` | true    | Include numeric characters (0-9)         |

### Cache Options

See `DistributedCacheEntryOptions` documentation for a complete list of configuration options.

| Property                          | Default   | Description                  |
| --------------------------------- | --------- | ---------------------------- |
| `AbsoluteExpirationRelativeToNow` | 5 minutes | How long tokens remain valid |

## Cache Key Format

The cache key format uses three components: `{tokenProviderName}_{purpose}_{uniqueIdentifier}`

### Examples:

#### Possible Email Token Provider Example

Email token provider uses:

- **Token Provider Name**: `"EmailToken"` (identifies the specific use case)
- **Purpose**: `"EmailTwoFactorAuthentication"` (specific action being verified)
- **Unique Identifier**: `"{user.Id}_{securityStamp}"` (user-specific data)

These are passed into the OTP Token Provider which creates a cache record:

- Cache Key: `EmailToken_EmailTwoFactorAuthentication_guid_guid`

## Security Considerations

### Token Generation

- Uses `CoreHelpers.SecureRandomString()` for cryptographically secure randomness
- No predictable patterns in generated tokens
- Configurable character sets for different security requirements

### Storage

- Tokens are stored in distributed cache. The cache depends on the specific deployment, for cloud it is CosmosDb.
- Automatic expiration prevents indefinite token validity
- One-time use prevents replay attacks

### Validation

- Exact string matching for validation
- Automatic removal after successful validation
- Returns `false` for expired or non-existent tokens

## Dependency Injection

The provider is registered in `ServiceCollectionExtensions.cs`:

```csharp
services.TryAddScoped<IOtpTokenProvider<TOptions>, OtpTokenProvider<TOptions>>();
```

## Error Handling

### Common Scenarios

- **Token Not Found**: `ValidateTokenAsync()` returns `false`
- **Token Expired**: Automatically cleaned up by cache, validation returns `false`
- **Invalid Input**:
  - `GenerateTokenAsync` returns `null` for empty/null tokenProviderName, purpose, or uniqueIdentifier
  - `ValidateTokenAsync` returns `false` for empty/null token, tokenProviderName, purpose, or uniqueIdentifier
  - No cache operations are performed for invalid inputs

### Best Practices

- Always check validation results
- Handle token expiration gracefully
- Provide clear user feedback for invalid tokens
- Implement rate limiting for token generation

## Related Components

- **`CoreHelpers.SecureRandomString()`**: Secure token generation
- **`IDistributedCache`**: Token storage backend
- **Two-Factor Authentication Providers**: Integration with 2FA flows
- **Email Services**: A Token delivery mechanism

## Testing

When testing components that use `OtpTokenProvider`:

```csharp
// Mock the interface for unit tests
var mockOtpProvider = Substitute.For<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>();
mockOtpProvider.GenerateTokenAsync("EmailToken", "email_verification", "user_123").Returns("123456");
mockOtpProvider.ValidateTokenAsync("123456", "EmailToken", "email_verification", "user_123").Returns(true);
```
