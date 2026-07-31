using AutoFixture;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Api.IntegrationTest.KeyManagement;

/// <summary>
/// Gives request models a valid hex-encoded <c>UserKeyId</c>.
/// </summary>
internal class KeyIdRequestCustomization : ICustomization
{
    private readonly string _hexEncodedKeyId = Guid.NewGuid().ToString("N");

    public void Customize(IFixture fixture)
    {
        fixture.Customize<MasterPasswordUnlockAndAuthenticationDataModel>(composer => composer
            .With(o => o.UserKeyId, _hexEncodedKeyId));
        fixture.Customize<RotateUserKeysRequestModel>(composer => composer
            .With(o => o.UserKeyId, _hexEncodedKeyId));
        fixture.Customize<RotateUserAccountKeysAndDataRequestModel>(composer => composer
            .With(o => o.UserKeyId, _hexEncodedKeyId));
        fixture.Customize<SetUserKeyIdRequestModel>(composer => composer
            .With(o => o.UserKeyId, _hexEncodedKeyId));
        fixture.Customize<SetKeyConnectorKeyRequestModel>(composer => composer
            .With(o => o.UserKeyId, _hexEncodedKeyId));
    }
}

public class KeyIdRequestCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new KeyIdRequestCustomization();
}
