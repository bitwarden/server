using System.Text.Json;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.Identity;

[SutProviderCustomize]
public class OtpTokenProviderTests
{
    private readonly string _defaultTokenProviderName = "DefaultOtpProvider";

    private readonly DefaultOtpTokenProviderOptions _defaultOtpTokenProviderOptions = new()
    {
        TokenLength = 6,
        TokenAlpha = false,
        TokenNumeric = true
    };

    /// <summary>
    /// Mirrors the private cache entry shape <see cref="OtpTokenProvider{TOptions}"/> stores internally, so
    /// tests can stand up a cached value without depending on that type directly.
    /// </summary>
    private static byte[] SerializedEntry(string token, string? boundValue = null)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new { Token = token, BoundValue = boundValue });
    }

    [Theory, BitAutoData]
    public async Task GenerateTokenAsync_Success_ReturnsToken(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose,
        string uniqueIdentifier)
    {
        // Arrange
        sutProvider.GetDependency<IOptions<DefaultOtpTokenProviderOptions>>()
            .Value.Returns(_defaultOtpTokenProviderOptions);
        sutProvider.Create();

        // Act
        var result = await sutProvider.Sut.GenerateTokenAsync(_defaultTokenProviderName, purpose, uniqueIdentifier);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Equal(6, result.Length); // Default length
        Assert.True(result.All(char.IsDigit)); // Default is numeric only

        // Verify cache was called with correct key
        var expectedCacheKey = $"{_defaultTokenProviderName}_{purpose}_{uniqueIdentifier}";
        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .SetAsync(expectedCacheKey, Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>());
    }

    [Theory, BitAutoData]
    public async Task GenerateTokenAsync_CustomConfiguration_ReturnsCorrectFormat(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string tokenProviderName,
        string purpose,
        string uniqueIdentifier)
    {
        // Arrange
        var otpConfig = new DefaultOtpTokenProviderOptions
        {
            TokenLength = 8,
            TokenAlpha = true,
            TokenNumeric = true
        };

        sutProvider.GetDependency<IOptions<DefaultOtpTokenProviderOptions>>()
            .Value.Returns(otpConfig);
        sutProvider.Create();

        // Act
        var result = await sutProvider.Sut.GenerateTokenAsync(tokenProviderName, purpose, uniqueIdentifier);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(8, result.Length);
        Assert.Contains(result, char.IsLetterOrDigit);
    }


    [Theory, BitAutoData]
    public async Task GenerateTokenAsync_NumericOnly_ReturnsOnlyDigits(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose,
        string uniqueIdentifier)
    {
        // Arrange
        var otpConfig = new DefaultOtpTokenProviderOptions
        {
            TokenLength = 10,
            TokenAlpha = false,
            TokenNumeric = true
        };

        sutProvider.GetDependency<IOptions<DefaultOtpTokenProviderOptions>>()
            .Value.Returns(otpConfig);
        sutProvider.Create();

        // Act
        var result = await sutProvider.Sut.GenerateTokenAsync(_defaultTokenProviderName, purpose, uniqueIdentifier);

        // Assert
        Assert.Equal(10, result.Length);
        Assert.True(result.All(char.IsDigit));
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_ValidToken_ReturnsTrue(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose,
        string uniqueIdentifier,
        string token)
    {
        // Arrange
        var expectedCacheKey = $"{_defaultTokenProviderName}_{purpose}_{uniqueIdentifier}";

        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedCacheKey)
            .Returns(SerializedEntry(token));

        // Act
        var result = await sutProvider.Sut.ValidateTokenAsync(token, _defaultTokenProviderName, purpose, uniqueIdentifier);

        // Assert
        Assert.True(result);

        // Verify token was removed from cache after successful validation
        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .RemoveAsync(expectedCacheKey);
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_InvalidToken_ReturnsFalse(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose,
        string uniqueIdentifier,
        string token,
        string wrongToken)
    {
        // Arrange
        var expectedCacheKey = $"{_defaultTokenProviderName}_{purpose}_{uniqueIdentifier}";

        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedCacheKey)
            .Returns(SerializedEntry(wrongToken)); // Different token in cache

        // Act
        var result = await sutProvider.Sut.ValidateTokenAsync(token, _defaultTokenProviderName, purpose, uniqueIdentifier);

        // Assert
        Assert.False(result);

        // Verify token was NOT removed from cache for invalid validation
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .RemoveAsync(expectedCacheKey);
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_TokenNotFound_ReturnsFalse(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose,
        string uniqueIdentifier,
        string token)
    {
        // Arrange
        var expectedCacheKey = $"{_defaultTokenProviderName}_{purpose}_{uniqueIdentifier}";

        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedCacheKey)
            .Returns((byte[])null); // Token not found in cache

        // Act
        var result = await sutProvider.Sut.ValidateTokenAsync(token, _defaultTokenProviderName, purpose, uniqueIdentifier);

        // Assert
        Assert.False(result);

        // Verify removal was not attempted
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_EmptyToken_ReturnsFalse(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose,
        string uniqueIdentifier)
    {
        // Act
        var result = await sutProvider.Sut.ValidateTokenAsync("", _defaultTokenProviderName, purpose, uniqueIdentifier);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_NullToken_ReturnsFalse(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose,
        string uniqueIdentifier)
    {
        // Act
        var result = await sutProvider.Sut.ValidateTokenAsync(null, _defaultTokenProviderName, purpose, uniqueIdentifier);

        // Assert
        Assert.False(result);
    }

    // Tests for null/empty purpose and uniqueIdentifier parameters
    [Theory, BitAutoData]
    public async Task GenerateTokenAsync_NullPurpose_ReturnsNull(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string uniqueIdentifier)
    {
        // Act
        var result = await sutProvider.Sut.GenerateTokenAsync(_defaultTokenProviderName, null, uniqueIdentifier);

        // Assert
        Assert.Null(result);

        // Verify cache was not called
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>());
    }

    [Theory, BitAutoData]
    public async Task GenerateTokenAsync_EmptyPurpose_ReturnsNull(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string uniqueIdentifier)
    {
        // Act
        var result = await sutProvider.Sut.GenerateTokenAsync(_defaultTokenProviderName, "", uniqueIdentifier);

        // Assert
        Assert.Null(result);

        // Verify cache was not called
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>());
    }

    [Theory, BitAutoData]
    public async Task GenerateTokenAsync_NullUniqueIdentifier_ReturnsNull(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose)
    {
        // Act
        var result = await sutProvider.Sut.GenerateTokenAsync(_defaultTokenProviderName, purpose, null);

        // Assert
        Assert.Null(result);

        // Verify cache was not called
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>());
    }

    [Theory, BitAutoData]
    public async Task GenerateTokenAsync_EmptyUniqueIdentifier_ReturnsNull(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose)
    {
        // Act
        var result = await sutProvider.Sut.GenerateTokenAsync(_defaultTokenProviderName, purpose, "");

        // Assert
        Assert.Null(result);

        // Verify cache was not called
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>());
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_NullPurpose_ReturnsFalse(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string token,
        string uniqueIdentifier)
    {
        // Act
        var result = await sutProvider.Sut.ValidateTokenAsync(token, _defaultTokenProviderName, null, uniqueIdentifier);

        // Assert
        Assert.False(result);

        // Verify cache was not called
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .GetAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_EmptyPurpose_ReturnsFalse(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string token,
        string uniqueIdentifier)
    {
        // Act
        var result = await sutProvider.Sut.ValidateTokenAsync(token, _defaultTokenProviderName, "", uniqueIdentifier);

        // Assert
        Assert.False(result);

        // Verify cache was not called
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .GetAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_NullUniqueIdentifier_ReturnsFalse(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string token,
        string purpose)
    {
        // Act
        var result = await sutProvider.Sut.ValidateTokenAsync(token, _defaultTokenProviderName, purpose, null);

        // Assert
        Assert.False(result);

        // Verify cache was not called
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .GetAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_EmptyUniqueIdentifier_ReturnsFalse(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string token,
        string purpose)
    {
        // Act
        var result = await sutProvider.Sut.ValidateTokenAsync(token, _defaultTokenProviderName, purpose, "");

        // Assert
        Assert.False(result);

        // Verify cache was not called
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .GetAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task GenerateTokenAsync_OverwritesExistingToken(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose,
        string uniqueIdentifier)
    {
        // Arrange
        sutProvider.GetDependency<IOptions<DefaultOtpTokenProviderOptions>>()
            .Value.Returns(_defaultOtpTokenProviderOptions);
        sutProvider.Create();

        // Act - Generate token twice with same parameters
        var firstToken = await sutProvider.Sut.GenerateTokenAsync(_defaultTokenProviderName, purpose, uniqueIdentifier);
        var secondToken = await sutProvider.Sut.GenerateTokenAsync(_defaultTokenProviderName, purpose, uniqueIdentifier);

        // Assert
        Assert.NotEqual(firstToken, secondToken); // Should be different tokens

        var expectedCacheKey = $"{_defaultTokenProviderName}_{purpose}_{uniqueIdentifier}";
        await sutProvider.GetDependency<IDistributedCache>()
            .Received(2) // Called twice - once for each generation
            .SetAsync(expectedCacheKey, Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>());
    }

    [Theory, BitAutoData]
    public async Task CacheKeyFormat_IsCorrect(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose,
        string uniqueIdentifier)
    {
        // Arrange
        sutProvider.GetDependency<IOptions<DefaultOtpTokenProviderOptions>>()
            .Value.Returns(_defaultOtpTokenProviderOptions);
        sutProvider.Create();

        // Act
        await sutProvider.Sut.GenerateTokenAsync(_defaultTokenProviderName, purpose, uniqueIdentifier);

        // Assert
        var expectedCacheKey = $"{_defaultTokenProviderName}_{purpose}_{uniqueIdentifier}";
        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .SetAsync(expectedCacheKey, Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>());
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_CaseSensitive(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose,
        string uniqueIdentifier)
    {
        // Arrange
        var token = "ABC123";
        var expectedCacheKey = $"{_defaultTokenProviderName}_{purpose}_{uniqueIdentifier}";
        var entryBytes = SerializedEntry(token);

        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedCacheKey)
            .Returns(entryBytes);

        // Act & Assert
        var validResult = await sutProvider.Sut.ValidateTokenAsync("ABC123", _defaultTokenProviderName, purpose, uniqueIdentifier);
        Assert.True(validResult);

        // Reset the cache mock to return the token again
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedCacheKey)
            .Returns(entryBytes);

        var invalidResult = await sutProvider.Sut.ValidateTokenAsync("abc123", _defaultTokenProviderName, purpose, uniqueIdentifier);
        Assert.False(invalidResult);
    }

    [Theory, BitAutoData]
    public async Task RoundTrip_GenerateAndValidate_Success(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose,
        string uniqueIdentifier)
    {
        // Arrange
        sutProvider.GetDependency<IOptions<DefaultOtpTokenProviderOptions>>()
            .Value.Returns(_defaultOtpTokenProviderOptions);
        sutProvider.Create();

        var expectedCacheKey = $"{_defaultTokenProviderName}_{purpose}_{uniqueIdentifier}";
        byte[] storedToken = null;

        // Setup cache to capture stored token and return it on get
        sutProvider.GetDependency<IDistributedCache>()
            .When(x => x.SetAsync(expectedCacheKey, Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>()))
            .Do(callInfo => storedToken = callInfo.ArgAt<byte[]>(1));

        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedCacheKey)
            .Returns(callInfo => storedToken);

        // Act
        var generatedToken = await sutProvider.Sut.GenerateTokenAsync(_defaultTokenProviderName, purpose, uniqueIdentifier);
        var isValid = await sutProvider.Sut.ValidateTokenAsync(generatedToken, _defaultTokenProviderName, purpose, uniqueIdentifier);

        // Assert
        Assert.True(isValid);
        Assert.NotNull(generatedToken);
        Assert.NotEmpty(generatedToken);
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_MatchingBoundValue_ReturnsTrue(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose,
        string uniqueIdentifier,
        string token,
        string boundValue)
    {
        var expectedCacheKey = $"{_defaultTokenProviderName}_{purpose}_{uniqueIdentifier}";

        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedCacheKey)
            .Returns(SerializedEntry(token, boundValue));

        var result = await sutProvider.Sut.ValidateTokenAsync(token, _defaultTokenProviderName, purpose, uniqueIdentifier, boundValue);

        Assert.True(result);
        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .RemoveAsync(expectedCacheKey);
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_MismatchedBoundValue_ReturnsFalse(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose,
        string uniqueIdentifier,
        string token,
        string boundValue,
        string otherBoundValue)
    {
        var expectedCacheKey = $"{_defaultTokenProviderName}_{purpose}_{uniqueIdentifier}";

        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedCacheKey)
            .Returns(SerializedEntry(token, boundValue));

        var result = await sutProvider.Sut.ValidateTokenAsync(token, _defaultTokenProviderName, purpose, uniqueIdentifier, otherBoundValue);

        Assert.False(result);
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_NoBoundValuePassed_RequiresNoBoundValueStored(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose,
        string uniqueIdentifier,
        string token,
        string boundValue)
    {
        var expectedCacheKey = $"{_defaultTokenProviderName}_{purpose}_{uniqueIdentifier}";

        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedCacheKey)
            .Returns(SerializedEntry(token, boundValue));

        var result = await sutProvider.Sut.ValidateTokenAsync(token, _defaultTokenProviderName, purpose, uniqueIdentifier);

        Assert.False(result);
    }

    /// <summary>
    /// A token generated the unbound way - the only shape that existed before <c>boundValue</c> was added,
    /// and still how <c>SendEmailOtpRequestValidator</c> generates its tokens today - must not accidentally
    /// validate for a caller that happens to pass a bound value.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_UnboundStoredValue_BoundValuePassed_ReturnsFalse(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose,
        string uniqueIdentifier,
        string token,
        string boundValue)
    {
        var expectedCacheKey = $"{_defaultTokenProviderName}_{purpose}_{uniqueIdentifier}";

        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedCacheKey)
            .Returns(SerializedEntry(token));

        var result = await sutProvider.Sut.ValidateTokenAsync(token, _defaultTokenProviderName, purpose, uniqueIdentifier, boundValue);

        Assert.False(result);
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
    }

    /// <summary>
    /// Confirms the existing unbound call shape - <c>SendEmailOtpRequestValidator</c>'s only usage - is
    /// unaffected by the addition of <c>boundValue</c>: neither side passes it, and validation still succeeds.
    /// </summary>
    [Theory, BitAutoData]
    public async Task GenerateAndValidate_NeitherSidePassesBoundValue_StillValidates(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose,
        string uniqueIdentifier)
    {
        sutProvider.GetDependency<IOptions<DefaultOtpTokenProviderOptions>>()
            .Value.Returns(_defaultOtpTokenProviderOptions);
        sutProvider.Create();

        var expectedCacheKey = $"{_defaultTokenProviderName}_{purpose}_{uniqueIdentifier}";
        byte[]? storedBytes = null;
        sutProvider.GetDependency<IDistributedCache>()
            .When(x => x.SetAsync(expectedCacheKey, Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>()))
            .Do(callInfo => storedBytes = callInfo.ArgAt<byte[]>(1));

        var token = await sutProvider.Sut.GenerateTokenAsync(_defaultTokenProviderName, purpose, uniqueIdentifier);

        sutProvider.GetDependency<IDistributedCache>().GetAsync(expectedCacheKey).Returns(storedBytes);
        var isValid = await sutProvider.Sut.ValidateTokenAsync(token, _defaultTokenProviderName, purpose, uniqueIdentifier);

        Assert.True(isValid);
    }

    // TODO: PM-43465 - Delete this test along with PeekBoundValueAsync once every supported client version
    // sends the Device-Identifier header on the new device verification resend request.
    [Theory, BitAutoData]
    public async Task PeekBoundValueAsync_Pending_ReturnsBoundValueWithoutConsuming(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose,
        string uniqueIdentifier,
        string token,
        string boundValue)
    {
        var expectedCacheKey = $"{_defaultTokenProviderName}_{purpose}_{uniqueIdentifier}";

        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedCacheKey)
            .Returns(SerializedEntry(token, boundValue));

        var result = await sutProvider.Sut.PeekBoundValueAsync(_defaultTokenProviderName, purpose, uniqueIdentifier);

        Assert.Equal(boundValue, result);
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
    }

    // TODO: PM-43465 - Delete this test along with PeekBoundValueAsync once every supported client version
    // sends the Device-Identifier header on the new device verification resend request.
    [Theory, BitAutoData]
    public async Task PeekBoundValueAsync_NotPending_ReturnsNull(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose,
        string uniqueIdentifier)
    {
        var expectedCacheKey = $"{_defaultTokenProviderName}_{purpose}_{uniqueIdentifier}";

        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedCacheKey)
            .Returns((byte[])null);

        var result = await sutProvider.Sut.PeekBoundValueAsync(_defaultTokenProviderName, purpose, uniqueIdentifier);

        Assert.Null(result);
    }

    /// <summary>
    /// Before <c>BoundValue</c> was added, <see cref="OtpTokenProvider{TOptions}"/> stored a bare UTF-8 token
    /// string rather than a JSON <c>OtpCacheEntry</c>. During a rolling deploy, instances still running the
    /// prior version keep writing that shape, so an upgraded instance must not throw when it reads one back.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ValidateTokenAsync_LegacyRawTokenCacheEntry_ReturnsFalse(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose,
        string uniqueIdentifier,
        string token)
    {
        var expectedCacheKey = $"{_defaultTokenProviderName}_{purpose}_{uniqueIdentifier}";

        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedCacheKey)
            .Returns(System.Text.Encoding.UTF8.GetBytes(token));

        var result = await sutProvider.Sut.ValidateTokenAsync(token, _defaultTokenProviderName, purpose, uniqueIdentifier);

        Assert.False(result);
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task GenerateTokenAsync_WithBoundValue_StoresIt(
        SutProvider<OtpTokenProvider<DefaultOtpTokenProviderOptions>> sutProvider,
        string purpose,
        string uniqueIdentifier,
        string boundValue)
    {
        sutProvider.GetDependency<IOptions<DefaultOtpTokenProviderOptions>>()
            .Value.Returns(_defaultOtpTokenProviderOptions);
        sutProvider.Create();

        var expectedCacheKey = $"{_defaultTokenProviderName}_{purpose}_{uniqueIdentifier}";
        byte[]? storedBytes = null;
        sutProvider.GetDependency<IDistributedCache>()
            .When(x => x.SetAsync(expectedCacheKey, Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>()))
            .Do(callInfo => storedBytes = callInfo.ArgAt<byte[]>(1));

        await sutProvider.Sut.GenerateTokenAsync(_defaultTokenProviderName, purpose, uniqueIdentifier, boundValue);

        sutProvider.GetDependency<IDistributedCache>().GetAsync(expectedCacheKey).Returns(storedBytes);
        var peeked = await sutProvider.Sut.PeekBoundValueAsync(_defaultTokenProviderName, purpose, uniqueIdentifier);
        Assert.Equal(boundValue, peeked);
    }
}
