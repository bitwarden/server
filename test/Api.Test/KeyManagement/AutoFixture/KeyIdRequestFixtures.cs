using AutoFixture;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Api.Test.KeyManagement.AutoFixture;

/// <summary>
/// Gives this assembly's request models a valid hex-encoded key id.
/// <para>
/// An arbitrary generated string is not valid hex, so converting one to a key id throws. Every key id
/// in one fixture is the same value, since a rotation request is only coherent when the key ids it
/// carries all name one key.
/// </para>
/// <para>
/// The equivalent handling for Core request models lives in <c>Bit.Test.Common</c>'s
/// <c>KeyIdCustomization</c>, which cannot reach into this assembly.
/// </para>
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
