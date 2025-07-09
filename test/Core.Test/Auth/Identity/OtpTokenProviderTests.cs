using System.Text;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.Identity;

[SutProviderCustomize]
public class OtpTokenProviderTests
{
    [Theory, BitAutoData]
    public async Task GenerateTokenAsync_Success_ReturnsToken(
        SutProvider<OtpTokenProvider> sutProvider,
        string purpose,
        string uniqueIdentifier)
    {
        // Act
        var result = await sutProvider.Sut.GenerateTokenAsync(purpose, uniqueIdentifier);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Equal(6, result.Length); // Default length
        Assert.True(result.All(char.IsDigit)); // Default is numeric only

        // Verify cache was called with correct key
        var expectedCacheKey = $"{purpose}_{uniqueIdentifier}";
        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .SetAsync(expectedCacheKey, Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>());
    }

    [Theory, BitAutoData]
    public async Task GenerateTokenAsync_CustomConfiguration_ReturnsCorrectFormat(
        SutProvider<OtpTokenProvider> sutProvider,
        string purpose,
        string uniqueIdentifier)
    {
        // Arrange
        sutProvider.Sut.ConfigureToken(8, true, true); // 8 chars, alpha + numeric

        // Act
        var result = await sutProvider.Sut.GenerateTokenAsync(purpose, uniqueIdentifier);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(8, result.Length);
        Assert.Contains(result, char.IsLetterOrDigit);
    }


    [Theory, BitAutoData]
    public async Task GenerateTokenAsync_NumericOnly_ReturnsOnlyDigits(
        SutProvider<OtpTokenProvider> sutProvider,
        string purpose,
        string uniqueIdentifier)
    {
        // Arrange
        sutProvider.Sut.ConfigureToken(10, false, true); // 10 chars, numeric only

        // Act
        var result = await sutProvider.Sut.GenerateTokenAsync(purpose, uniqueIdentifier);

        // Assert
        Assert.Equal(10, result.Length);
        Assert.True(result.All(char.IsDigit));
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_ValidToken_ReturnsTrue(
        SutProvider<OtpTokenProvider> sutProvider,
        string purpose,
        string uniqueIdentifier,
        string token)
    {
        // Arrange
        var expectedCacheKey = $"{purpose}_{uniqueIdentifier}";
        var tokenBytes = Encoding.UTF8.GetBytes(token);

        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedCacheKey)
            .Returns(tokenBytes);

        // Act
        var result = await sutProvider.Sut.ValidateTokenAsync(token, purpose, uniqueIdentifier);

        // Assert
        Assert.True(result);

        // Verify token was removed from cache after successful validation
        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .RemoveAsync(expectedCacheKey);
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_InvalidToken_ReturnsFalse(
        SutProvider<OtpTokenProvider> sutProvider,
        string purpose,
        string uniqueIdentifier,
        string token,
        string wrongToken)
    {
        // Arrange
        var expectedCacheKey = $"{purpose}_{uniqueIdentifier}";
        var tokenBytes = Encoding.UTF8.GetBytes(wrongToken); // Different token in cache

        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedCacheKey)
            .Returns(tokenBytes);

        // Act
        var result = await sutProvider.Sut.ValidateTokenAsync(token, purpose, uniqueIdentifier);

        // Assert
        Assert.False(result);

        // Verify token was NOT removed from cache for invalid validation
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .RemoveAsync(expectedCacheKey);
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_TokenNotFound_ReturnsFalse(
        SutProvider<OtpTokenProvider> sutProvider,
        string purpose,
        string uniqueIdentifier,
        string token)
    {
        // Arrange
        var expectedCacheKey = $"{purpose}_{uniqueIdentifier}";

        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedCacheKey)
            .Returns((byte[])null); // Token not found in cache

        // Act
        var result = await sutProvider.Sut.ValidateTokenAsync(token, purpose, uniqueIdentifier);

        // Assert
        Assert.False(result);

        // Verify removal was not attempted
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_EmptyToken_ReturnsFalse(
        SutProvider<OtpTokenProvider> sutProvider,
        string purpose,
        string uniqueIdentifier)
    {
        // Act
        var result = await sutProvider.Sut.ValidateTokenAsync("", purpose, uniqueIdentifier);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_NullToken_ReturnsFalse(
        SutProvider<OtpTokenProvider> sutProvider,
        string purpose,
        string uniqueIdentifier)
    {
        // Act
        var result = await sutProvider.Sut.ValidateTokenAsync(null, purpose, uniqueIdentifier);

        // Assert
        Assert.False(result);
    }

    // Tests for null/empty purpose and uniqueIdentifier parameters
    [Theory, BitAutoData]
    public async Task GenerateTokenAsync_NullPurpose_ReturnsNull(
        SutProvider<OtpTokenProvider> sutProvider,
        string uniqueIdentifier)
    {
        // Act
        var result = await sutProvider.Sut.GenerateTokenAsync(null, uniqueIdentifier);

        // Assert
        Assert.Null(result);

        // Verify cache was not called
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>());
    }

    [Theory, BitAutoData]
    public async Task GenerateTokenAsync_EmptyPurpose_ReturnsNull(
        SutProvider<OtpTokenProvider> sutProvider,
        string uniqueIdentifier)
    {
        // Act
        var result = await sutProvider.Sut.GenerateTokenAsync("", uniqueIdentifier);

        // Assert
        Assert.Null(result);

        // Verify cache was not called
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>());
    }

    [Theory, BitAutoData]
    public async Task GenerateTokenAsync_NullUniqueIdentifier_ReturnsNull(
        SutProvider<OtpTokenProvider> sutProvider,
        string purpose)
    {
        // Act
        var result = await sutProvider.Sut.GenerateTokenAsync(purpose, null);

        // Assert
        Assert.Null(result);

        // Verify cache was not called
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>());
    }

    [Theory, BitAutoData]
    public async Task GenerateTokenAsync_EmptyUniqueIdentifier_ReturnsNull(
        SutProvider<OtpTokenProvider> sutProvider,
        string purpose)
    {
        // Act
        var result = await sutProvider.Sut.GenerateTokenAsync(purpose, "");

        // Assert
        Assert.Null(result);

        // Verify cache was not called
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>());
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_NullPurpose_ReturnsFalse(
        SutProvider<OtpTokenProvider> sutProvider,
        string token,
        string uniqueIdentifier)
    {
        // Act
        var result = await sutProvider.Sut.ValidateTokenAsync(token, null, uniqueIdentifier);

        // Assert
        Assert.False(result);

        // Verify cache was not called
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .GetAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_EmptyPurpose_ReturnsFalse(
        SutProvider<OtpTokenProvider> sutProvider,
        string token,
        string uniqueIdentifier)
    {
        // Act
        var result = await sutProvider.Sut.ValidateTokenAsync(token, "", uniqueIdentifier);

        // Assert
        Assert.False(result);

        // Verify cache was not called
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .GetAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_NullUniqueIdentifier_ReturnsFalse(
        SutProvider<OtpTokenProvider> sutProvider,
        string token,
        string purpose)
    {
        // Act
        var result = await sutProvider.Sut.ValidateTokenAsync(token, purpose, null);

        // Assert
        Assert.False(result);

        // Verify cache was not called
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .GetAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_EmptyUniqueIdentifier_ReturnsFalse(
        SutProvider<OtpTokenProvider> sutProvider,
        string token,
        string purpose)
    {
        // Act
        var result = await sutProvider.Sut.ValidateTokenAsync(token, purpose, "");

        // Assert
        Assert.False(result);

        // Verify cache was not called
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .GetAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task GenerateTokenAsync_OverwritesExistingToken(
        SutProvider<OtpTokenProvider> sutProvider,
        string purpose,
        string uniqueIdentifier)
    {
        // Act - Generate token twice with same parameters
        var firstToken = await sutProvider.Sut.GenerateTokenAsync(purpose, uniqueIdentifier);
        var secondToken = await sutProvider.Sut.GenerateTokenAsync(purpose, uniqueIdentifier);

        // Assert
        Assert.NotEqual(firstToken, secondToken); // Should be different tokens

        var expectedCacheKey = $"{purpose}_{uniqueIdentifier}";
        await sutProvider.GetDependency<IDistributedCache>()
            .Received(2) // Called twice - once for each generation
            .SetAsync(expectedCacheKey, Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>());
    }

    [Theory, BitAutoData]
    public void ConfigureToken_UpdatesProperties(
        SutProvider<OtpTokenProvider> sutProvider)
    {
        // Arrange
        var length = 12;
        var alpha = true;
        var numeric = false;

        // Act
        sutProvider.Sut.ConfigureToken(length, alpha, numeric);

        // Assert
        Assert.Equal(length, sutProvider.Sut.TokenLength);
        Assert.Equal(alpha, sutProvider.Sut.TokenAlpha);
        Assert.Equal(numeric, sutProvider.Sut.TokenNumeric);
    }

    [Theory, BitAutoData]
    public void SetCacheEntryOptions_UpdatesOptions(
        SutProvider<OtpTokenProvider> sutProvider)
    {
        // Arrange
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        };

        // Act
        sutProvider.Sut.SetCacheEntryOptions(options);

        // Assert
        Assert.Equal(options, sutProvider.Sut._distributedCacheEntryOptions);
    }

    [Theory, BitAutoData]
    public async Task CacheKeyFormat_IsCorrect(
        SutProvider<OtpTokenProvider> sutProvider,
        string purpose,
        string uniqueIdentifier)
    {
        // Act
        await sutProvider.Sut.GenerateTokenAsync(purpose, uniqueIdentifier);

        // Assert
        var expectedCacheKey = $"{purpose}_{uniqueIdentifier}";
        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .SetAsync(expectedCacheKey, Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>());
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_CaseSensitive(
        SutProvider<OtpTokenProvider> sutProvider,
        string purpose,
        string uniqueIdentifier)
    {
        // Arrange
        var token = "ABC123";
        var expectedCacheKey = $"{purpose}_{uniqueIdentifier}";
        var tokenBytes = Encoding.UTF8.GetBytes(token);

        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedCacheKey)
            .Returns(tokenBytes);

        // Act & Assert
        var validResult = await sutProvider.Sut.ValidateTokenAsync("ABC123", purpose, uniqueIdentifier);
        Assert.True(validResult);

        // Reset the cache mock to return the token again
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedCacheKey)
            .Returns(tokenBytes);

        var invalidResult = await sutProvider.Sut.ValidateTokenAsync("abc123", purpose, uniqueIdentifier);
        Assert.False(invalidResult);
    }

    [Theory, BitAutoData]
    public async Task GenerateTokenAsync_WithCustomCacheOptions_UsesCorrectExpiration(
        SutProvider<OtpTokenProvider> sutProvider,
        string purpose,
        string uniqueIdentifier)
    {
        // Arrange
        var customExpiration = TimeSpan.FromHours(2);
        var customOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = customExpiration
        };
        sutProvider.Sut.SetCacheEntryOptions(customOptions);

        // Act
        await sutProvider.Sut.GenerateTokenAsync(purpose, uniqueIdentifier);

        // Assert
        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(),
                Arg.Is<DistributedCacheEntryOptions>(opts =>
                    opts.AbsoluteExpirationRelativeToNow == customExpiration));
    }

    [Theory, BitAutoData]
    public async Task RoundTrip_GenerateAndValidate_Success(
        SutProvider<OtpTokenProvider> sutProvider,
        string purpose,
        string uniqueIdentifier)
    {
        // Arrange
        var expectedCacheKey = $"{purpose}_{uniqueIdentifier}";
        byte[] storedToken = null;

        // Setup cache to capture stored token and return it on get
        sutProvider.GetDependency<IDistributedCache>()
            .When(x => x.SetAsync(expectedCacheKey, Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>()))
            .Do(callInfo => storedToken = callInfo.ArgAt<byte[]>(1));

        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedCacheKey)
            .Returns(callInfo => storedToken);

        // Act
        var generatedToken = await sutProvider.Sut.GenerateTokenAsync(purpose, uniqueIdentifier);
        var isValid = await sutProvider.Sut.ValidateTokenAsync(generatedToken, purpose, uniqueIdentifier);

        // Assert
        Assert.True(isValid);
        Assert.NotNull(generatedToken);
        Assert.NotEmpty(generatedToken);
    }
}
