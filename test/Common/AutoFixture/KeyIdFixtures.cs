using AutoFixture;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Test.Common.AutoFixture;

/// <summary>
/// Teaches AutoFixture how to deal with key ids.
/// <para>
/// <see cref="KeyId"/> is only constructible through a validating factory, so AutoFixture cannot
/// build one on its own. Request models carry the same value as a hex string, and an arbitrary
/// generated string is not valid hex, so converting it throws.
/// </para>
/// <para>
/// Every key id in a single fixture is the *same* value. A request is only coherent when the key ids
/// it carries all name one key, so that is what generated data should look like by default; tests
/// that need a mismatch set the differing value explicitly.
/// </para>
/// <para>
/// Applied to every <c>BitAutoData</c> fixture, since key ids appear on data objects that are
/// auto-generated throughout the test suite.
/// </para>
/// </summary>
public class KeyIdCustomization : ICustomization
{
    /// <summary>
    /// A Guid in "N" format is exactly 32 lowercase hex characters, which is the key id format.
    /// </summary>
    private readonly string _hexEncodedKeyId = Guid.NewGuid().ToString("N");

    public void Customize(IFixture fixture)
    {
        fixture.Register(() => KeyId.FromHexEncodedString(_hexEncodedKeyId));

        // Entities and request models store the key id as a hex string, and an arbitrary generated
        // string is not valid hex. Anything that converts one would otherwise throw.
        fixture.Customize<User>(composer => composer
            .With(o => o.UserKeyId, _hexEncodedKeyId));
        fixture.Customize<MasterPasswordUnlockDataRequestModel>(composer => composer
            .With(o => o.UserKeyId, _hexEncodedKeyId));
    }
}

public class KeyIdCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new KeyIdCustomization();
}
