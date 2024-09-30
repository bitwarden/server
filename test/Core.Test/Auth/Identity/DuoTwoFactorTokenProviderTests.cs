using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Auth.Identity;

public class DuoTwoFactorTokenProviderTests : BaseTokenProviderTests<DuoUniversalTokenProvider>
{
    public override TwoFactorProviderType TwoFactorProviderType => TwoFactorProviderType.Duo;

    public static IEnumerable<object[]> CanGenerateTwoFactorTokenAsyncData
        => SetupCanGenerateData(
            (
                new Dictionary<string, object>
                {
                    ["ClientId"] = "ClientId",
                    ["ClientSecret"] = "ClientSecret",
                    ["Host"] = "api-abcd1234.duosecurity.com",
                },
                true
            ),
            (
                new Dictionary<string, object>
                {
                    ["ClientId"] = "ClientId",
                    ["ClientSecret"] = "ClientSecret",
                    ["Host"] = "api-abcd1234.duofederal.com",
                },
                true
            ),
            (
                new Dictionary<string, object>
                {
                    ["ClientId"] = "ClientId",
                    ["ClientSecret"] = "ClientSecret",
                    ["Host"] = "",
                },
                false
            ),
            (
                new Dictionary<string, object>
                {
                    ["ClientSecret"] = "ClientSecret",
                    ["Host"] = "api-abcd1234.duofederal.com",
                },
                false
            )
        );

    [Theory, BitMemberAutoData(nameof(CanGenerateTwoFactorTokenAsyncData))]
    public override async Task RunCanGenerateTwoFactorTokenAsync(Dictionary<string, object> metaData, bool expectedResponse,
        User user, SutProvider<DuoUniversalTokenProvider> sutProvider)
    {
        user.Premium = true;
        user.PremiumExpirationDate = DateTime.UtcNow.AddDays(1);
        await base.RunCanGenerateTwoFactorTokenAsync(metaData, expectedResponse, user, sutProvider);
    }
}
