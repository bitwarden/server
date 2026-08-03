using AutoFixture;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Api.IntegrationTest.KeyManagement;

/// <summary>
/// Gives request models a valid hex-encoded <c>UserKeyId</c>.
/// </summary>
internal class KeyIdRequestCustomization : ICustomization
{
    // Shared with Bit.Test.Common's KeyIdCustomization so that a Core model and an API request
    // model built by the same test name the same key.
    private const string HexEncodedKeyId = KeyIdCustomization.HexEncodedKeyId;

    public void Customize(IFixture fixture)
    {
        fixture.Customize<MasterPasswordUnlockAndAuthenticationDataModel>(composer => composer
            .With(o => o.UserKeyId, HexEncodedKeyId));
        fixture.Customize<RotateUserKeysRequestModel>(composer => composer
            .With(o => o.UserKeyId, HexEncodedKeyId));
        fixture.Customize<RotateUserAccountKeysAndDataRequestModel>(composer => composer
            .With(o => o.UserKeyId, HexEncodedKeyId));
        fixture.Customize<SetUserKeyIdRequestModel>(composer => composer
            .With(o => o.UserKeyId, HexEncodedKeyId));
        fixture.Customize<SetKeyConnectorKeyRequestModel>(composer => composer
            .With(o => o.UserKeyId, HexEncodedKeyId));
    }
}

public class KeyIdRequestCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new KeyIdRequestCustomization();
}
