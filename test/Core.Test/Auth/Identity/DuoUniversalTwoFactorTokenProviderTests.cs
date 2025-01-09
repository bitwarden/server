using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using Duo = DuoUniversal;

namespace Bit.Core.Test.Auth.Identity;

public class DuoUniversalTwoFactorTokenProviderTests : BaseTokenProviderTests<DuoUniversalTokenProvider>
{
    private readonly IDuoUniversalTokenService _duoUniversalTokenService = Substitute.For<IDuoUniversalTokenService>();
    public override TwoFactorProviderType TwoFactorProviderType => TwoFactorProviderType.Duo;

    public static IEnumerable<object[]> CanGenerateTwoFactorTokenAsyncData
        => SetupCanGenerateData(
            ( // correct data
                new Dictionary<string, object>
                {
                    ["ClientId"] = new string('c', 20),
                    ["ClientSecret"] = new string('s', 40),
                    ["Host"] = "https://api-abcd1234.duosecurity.com",
                },
                true
            ),
            ( // correct data duo federal
                new Dictionary<string, object>
                {
                    ["ClientId"] = new string('c', 20),
                    ["ClientSecret"] = new string('s', 40),
                    ["Host"] = "https://api-abcd1234.duofederal.com",
                },
                true
            ),
            ( // correct data duo federal
                new Dictionary<string, object>
                {
                    ["ClientId"] = new string('c', 20),
                    ["ClientSecret"] = new string('s', 40),
                    ["Host"] = "https://api-abcd1234.duofederal.com",
                },
                true
            ),
            ( // invalid host
                new Dictionary<string, object>
                {
                    ["ClientId"] = new string('c', 20),
                    ["ClientSecret"] = new string('s', 40),
                    ["Host"] = "",
                },
                false
            ),
            ( // clientId missing
                new Dictionary<string, object>
                {
                    ["ClientSecret"] = new string('s', 40),
                    ["Host"] = "https://api-abcd1234.duofederal.com",
                },
                false
            )
        );

    public static IEnumerable<object[]> NonPremiumCanGenerateTwoFactorTokenAsyncData
        => SetupCanGenerateData(
            ( // correct data
                new Dictionary<string, object>
                {
                    ["ClientId"] = new string('c', 20),
                    ["ClientSecret"] = new string('s', 40),
                    ["Host"] = "https://api-abcd1234.duosecurity.com",
                },
                false
            )
        );

    [Theory, BitMemberAutoData(nameof(CanGenerateTwoFactorTokenAsyncData))]
    public override async Task RunCanGenerateTwoFactorTokenAsync(Dictionary<string, object> metaData, bool expectedResponse,
        User user, SutProvider<DuoUniversalTokenProvider> sutProvider)
    {
        // Arrange
        user.Premium = true;
        user.PremiumExpirationDate = DateTime.UtcNow.AddDays(1);

        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .HasProperDuoMetadata(Arg.Any<TwoFactorProvider>())
            .Returns(expectedResponse);

        // Act
        // Assert
        await base.RunCanGenerateTwoFactorTokenAsync(metaData, expectedResponse, user, sutProvider);
    }

    [Theory, BitMemberAutoData(nameof(NonPremiumCanGenerateTwoFactorTokenAsyncData))]
    public async Task CanGenerateTwoFactorTokenAsync_UserCanNotAccessPremium_ReturnsNull(Dictionary<string, object> metaData, bool expectedResponse,
    User user, SutProvider<DuoUniversalTokenProvider> sutProvider)
    {
        // Arrange
        user.Premium = false;

        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .HasProperDuoMetadata(Arg.Any<TwoFactorProvider>())
            .Returns(expectedResponse);

        // Act
        // Assert
        await base.RunCanGenerateTwoFactorTokenAsync(metaData, expectedResponse, user, sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task GenerateToken_Success_ReturnsAuthUrl(
        User user, SutProvider<DuoUniversalTokenProvider> sutProvider, string authUrl)
    {
        // Arrange
        SetUpProperDuoUniversalTokenService(user, sutProvider);

        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .GenerateAuthUrl(
                Arg.Any<Duo.Client>(),
                Arg.Any<IDataProtectorTokenFactory<DuoUserStateTokenable>>(),
                user)
            .Returns(authUrl);

        // Act
        var token = await sutProvider.Sut.GenerateAsync("purpose", SubstituteUserManager(), user);

        // Assert
        Assert.NotNull(token);
        Assert.Equal(token, authUrl);
    }

    [Theory]
    [BitAutoData]
    public async Task GenerateToken_DuoClientNull_ReturnsNull(
        User user, SutProvider<DuoUniversalTokenProvider> sutProvider)
    {
        // Arrange
        user.Premium = true;
        user.TwoFactorProviders = GetTwoFactorDuoProvidersJson();
        AdditionalSetup(sutProvider, user);

        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .HasProperDuoMetadata(Arg.Any<TwoFactorProvider>())
            .Returns(true);

        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .BuildDuoTwoFactorClientAsync(Arg.Any<TwoFactorProvider>())
            .Returns(null as Duo.Client);

        // Act
        var token = await sutProvider.Sut.GenerateAsync("purpose", SubstituteUserManager(), user);

        // Assert
        Assert.Null(token);
    }

    [Theory]
    [BitAutoData]
    public async Task GenerateToken_UserCanNotAccessPremium_ReturnsNull(
    User user, SutProvider<DuoUniversalTokenProvider> sutProvider)
    {
        // Arrange
        user.Premium = false;
        user.TwoFactorProviders = GetTwoFactorDuoProvidersJson();
        AdditionalSetup(sutProvider, user);

        // Act
        var token = await sutProvider.Sut.GenerateAsync("purpose", SubstituteUserManager(), user);

        // Assert
        Assert.Null(token);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateToken_ValidToken_ReturnsTrue(
        User user, SutProvider<DuoUniversalTokenProvider> sutProvider, string token)
    {
        // Arrange
        SetUpProperDuoUniversalTokenService(user, sutProvider);

        sutProvider.GetDependency<IDuoUniversalTokenService>()
                .RequestDuoValidationAsync(
                    Arg.Any<Duo.Client>(),
                    Arg.Any<IDataProtectorTokenFactory<DuoUserStateTokenable>>(),
                    user,
                    token)
                .Returns(true);

        // Act
        var response = await sutProvider.Sut.ValidateAsync("purpose", token, SubstituteUserManager(), user);

        // Assert
        Assert.True(response);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateToken_DuoClientNull_ReturnsFalse(
    User user, SutProvider<DuoUniversalTokenProvider> sutProvider, string token)
    {
        user.Premium = true;
        user.TwoFactorProviders = GetTwoFactorDuoProvidersJson();
        AdditionalSetup(sutProvider, user);

        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .HasProperDuoMetadata(Arg.Any<TwoFactorProvider>())
            .Returns(true);

        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .BuildDuoTwoFactorClientAsync(Arg.Any<TwoFactorProvider>())
            .Returns(null as Duo.Client);

        // Act
        var result = await sutProvider.Sut.ValidateAsync("purpose", token, SubstituteUserManager(), user);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Ensures that the IDuoUniversalTokenService is properly setup for the test.
    /// This ensures that the private GetDuoClientAsync, and GetDuoTwoFactorProvider
    /// methods will return true enabling the test to execute on the correct path.
    /// </summary>
    /// <param name="user">user from calling test</param>
    /// <param name="sutProvider">self</param>
    private void SetUpProperDuoUniversalTokenService(User user, SutProvider<DuoUniversalTokenProvider> sutProvider)
    {
        user.Premium = true;
        user.TwoFactorProviders = GetTwoFactorDuoProvidersJson();
        var client = BuildDuoClient();

        AdditionalSetup(sutProvider, user);

        sutProvider.GetDependency<IDuoUniversalTokenService>()
                .HasProperDuoMetadata(Arg.Any<TwoFactorProvider>())
                .Returns(true);

        sutProvider.GetDependency<IDuoUniversalTokenService>()
                .BuildDuoTwoFactorClientAsync(Arg.Any<TwoFactorProvider>())
                .Returns(client);
    }

    private Duo.Client BuildDuoClient()
    {
        var clientId = new string('c', 20);
        var clientSecret = new string('s', 40);
        return new Duo.ClientBuilder(clientId, clientSecret, "api-abcd1234.duosecurity.com", "redirectUrl").Build();
    }

    private string GetTwoFactorDuoProvidersJson()
    {
        return
            "{\"2\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"secretClientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}";
    }
}
