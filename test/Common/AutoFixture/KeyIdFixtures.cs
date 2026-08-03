using AutoFixture;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Test.Common.AutoFixture;

/// <summary>
/// Teaches AutoFixture how to deal with key ids.
/// <para>
/// <see cref="KeyId"/> is only constructible through a validating factory, so AutoFixture cannot
/// build one on its own. Entities carry the same value as a hex string, and an arbitrary generated
/// string is not valid hex, so converting it throws.
/// </para>
/// <para>
/// Applied to every <c>BitAutoData</c> fixture, since key ids appear on data objects that are
/// auto-generated throughout the test suite.
/// </para>
/// </summary>
public class KeyIdCustomization : ICustomization
{
    /// <summary>
    /// The key id every generated object carries.
    /// <para>
    /// A request is only coherent when the key ids it carries all name one key, so that is what
    /// generated data looks like by default; tests that need a mismatch set the differing value
    /// explicitly. Shared rather than per-fixture so that a Core model and an API request model
    /// built by the same test agree — projects that cannot reference <c>Bit.Test.Common</c>'s
    /// customization define their own and reuse this value.
    /// </para>
    /// <para>
    /// A Guid in "N" format is exactly 32 lowercase hex characters, which is the key id format.
    /// </para>
    /// </summary>
    public const string HexEncodedKeyId = "3f8c1d29a45b47e6b18d0c7e2a95f431";

    public void Customize(IFixture fixture)
    {
        fixture.Register(() => KeyId.FromHexEncodedString(HexEncodedKeyId));

        // Entities store the key id as a hex string, and an arbitrary generated string is not valid
        // hex. Anything that converts one would otherwise throw.
        fixture.Customize<User>(composer => composer
            .With(o => o.UserKeyId, HexEncodedKeyId));
    }
}

public class KeyIdCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new KeyIdCustomization();
}
