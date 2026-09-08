using System.Reflection;
using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Test.Common.AutoFixture;

/// <summary>
/// A key id is only valid as a 32 character lowercase hex string, which AutoFixture's random strings never
/// are: <see cref="KeyId.FromHexEncodedString"/> throws, and request models annotated with
/// <see cref="KeyIdAttribute"/> fail model validation. This hands out a well-formed key id instead, for both
/// <see cref="KeyId"/> itself and the string properties that carry one.
/// <para>
/// The same key id is used everywhere, so a model that carries a key id in more than one place stays
/// internally consistent. Tests that care about specific or mismatched key ids set them explicitly.
/// </para>
/// </summary>
public class KeyIdBuilder : ISpecimenBuilder
{
    public const string HexEncodedKeyId = "0123456789abcdef0123456789abcdef";

    public object Create(object request, ISpecimenContext context)
    {
        if (request is Type type && type == typeof(KeyId))
        {
            return KeyId.FromHexEncodedString(HexEncodedKeyId)!;
        }

        if (request is PropertyInfo property
            && property.PropertyType == typeof(string)
            && property.GetCustomAttribute<KeyIdAttribute>() != null)
        {
            return HexEncodedKeyId;
        }

        return new NoSpecimen();
    }
}

public class KeyIdCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Insert(0, new KeyIdBuilder());
    }
}
