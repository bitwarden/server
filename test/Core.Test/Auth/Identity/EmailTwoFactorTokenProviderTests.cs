using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Auth.Identity;

public class EmailTwoFactorTokenProviderTests : BaseTokenProviderTests<EmailTwoFactorTokenProvider>
{
    public override TwoFactorProviderType TwoFactorProviderType => TwoFactorProviderType.Email;

    public static IEnumerable<object[]> CanGenerateTwoFactorTokenAsyncData =>
        SetupCanGenerateData(
            (new Dictionary<string, object> { ["Email"] = "test@email.com" }, true),
            (new Dictionary<string, object> { ["NotEmail"] = "value" }, false),
            (new Dictionary<string, object> { ["Email"] = "" }, false)
        );

    [Theory, BitMemberAutoData(nameof(CanGenerateTwoFactorTokenAsyncData))]
    public override async Task RunCanGenerateTwoFactorTokenAsync(
        Dictionary<string, object> metaData,
        bool expectedResponse,
        User user,
        SutProvider<EmailTwoFactorTokenProvider> sutProvider
    )
    {
        await base.RunCanGenerateTwoFactorTokenAsync(metaData, expectedResponse, user, sutProvider);
    }
}
