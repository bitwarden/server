# OtpTokenProvider

The `OtpTokenProvider` is a token provider service for generating and validating Time-Based one-time passwords (TOTP). It provides a secure way to create temporary tokens for various authentication and verification scenarios.

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

### Interface: `IOtpTokenProvider`

```csharp
public interface IOtpTokenProvider
{
    Task<string> GenerateTokenAsync(string purpose, string uniqueIdentifier);
    Task<bool> ValidateTokenAsync(string token, string purpose, string uniqueIdentifier);
    void ConfigureToken(int length, bool alpha, bool numeric);
    void SetCacheEntryOptions(DistributedCacheEntryOptions options);
}
```

### Implementation: `OtpTokenProvider`

The provider is initialized with:
- **Distributed Cache**: Storage backend for tokens

## Usage

### Basic Setup

If your class needs the use the `IOtpTokenProvider` you can inject it like any other injectable class from the DI.

### Generating a Token

```csharp
// Generate a new OTP with purpose and unique identifier
string token = await otpProvider.GenerateTokenAsync("EmailToken", $"{userId}_{securityStamp}_{purpose}");
// Returns: "123456" (6-digit numeric by default)
```

### Validating a Token

```csharp
// Validate user-provided token with same purpose and unique identifier used for generation
bool isValid = await otpProvider.ValidateTokenAsync("123456", "EmailToken", $"{userId}_{securityStamp}_{purpose}");
// Returns: true if valid, false otherwise
// Note: Valid tokens are automatically removed from cache
```

### Custom Configuration

```csharp
// Configure token properties
otpProvider.ConfigureToken(
    length: 8,        // 8 characters
    alpha: true,      // Include letters
    numeric: true     // Include numbers
);

// Custom cache expiration
otpProvider.SetCacheEntryOptions(new DistributedCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
});
```

## Configuration Options

### Token Properties

| Property | Default | Description |
|----------|---------|-------------|
| `TokenLength` | 6 | Number of characters in generated token |
| `TokenAlpha` | false | Include alphabetic characters (a-z, A-Z) |
| `TokenNumeric` | true | Include numeric characters (0-9) |

### Cache Options

See `DistributedCacheEntryOptions` documentation for a complete list of configuration options.

| Property | Default | Description |
|----------|---------|-------------|
| `AbsoluteExpirationRelativeToNow` | 5 minutes | How long tokens remain valid |

## Cache Key Format

The provider uses a cache key format: `{purpose}_{uniqueIdentifier}`

### Examples:

#### Possible Email Token Provider
Email token provider uses the `user.Id`, `securityStamp`, and `purpose` to create a unique key. This key can be passed into the OTP provider along with the specific use case purpose.

- Use Case Purpose: `EmailToken`
- Unique Key: `purpose_guid_guid`

These are passed into the Otp Token Provider which create record in the cache:
- `EmailToken_purpose_guid_guid`

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
services.AddScoped<IOtpTokenProvider, OtpTokenProvider>();
```

## Error Handling

### Common Scenarios

- **Token Not Found**: `ValidateTokenAsync()` returns `false`
- **Token Expired**: Automatically cleaned up by cache, validation returns `false`
- **Invalid Input**:
  - `GenerateTokenAsync` returns `null` for empty/null purpose or uniqueIdentifier
  - `ValidateTokenAsync` returns `false` for empty/null token, purpose, or uniqueIdentifier
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
var mockOtpProvider = Substitute.For<IOtpTokenProvider>();
mockOtpProvider.GenerateTokenAsync("email_verification", "user_123").Returns("123456");
mockOtpProvider.ValidateTokenAsync("123456", "email_verification", "user_123").Returns(true);
```
