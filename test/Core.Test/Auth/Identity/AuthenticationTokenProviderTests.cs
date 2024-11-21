using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Auth.Identity;

public class AuthenticationTokenProviderTests : BaseTokenProviderTests<AuthenticatorTokenProvider>
{
    public override TwoFactorProviderType TwoFactorProviderType => TwoFactorProviderType.Authenticator;

    public static IEnumerable<object[]> CanGenerateTwoFactorTokenAsyncData
        => SetupCanGenerateData(
            (
                new Dictionary<string, object>
                {
                    ["Key"] = "stuff",
                },
                true
            ),
            (
                new Dictionary<string, object>
                {
                    ["Key"] = ""
                },
                false
            )
        );

    [Theory, BitMemberAutoData(nameof(CanGenerateTwoFactorTokenAsyncData))]
    public override async Task RunCanGenerateTwoFactorTokenAsync(Dictionary<string, object> metaData, bool expectedResponse,
        User user, SutProvider<AuthenticatorTokenProvider> sutProvider)
    {
        await base.RunCanGenerateTwoFactorTokenAsync(metaData, expectedResponse, user, sutProvider);
    }
}
