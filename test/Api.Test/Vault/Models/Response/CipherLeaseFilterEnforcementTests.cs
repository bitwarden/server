using System.Reflection;
using Bit.Api.Vault.Models.Response;
using Bit.Core.Vault.Authorization;
using Xunit;

namespace Bit.Api.Test.Vault.Models.Response;

/// <summary>
/// Architecture (fitness) tests for PAM credential-leasing filtering. The primary guarantee is enforced
/// by the type system: secret data on a cipher response can only be populated through a
/// <see cref="FullCipherAccess"/> witness that the leasing gate alone mints. These tests lock that
/// invariant in place so a future change that re-opens a public setter — or makes the witness publicly
/// mintable — fails CI rather than silently leaking secrets.
/// </summary>
public class CipherLeaseFilterEnforcementTests
{
    // The secret members carried by the cipher response models. None may be settable by external code
    // (public setter / object initializer); they are populated only inside the witness-gated full path.
    public static readonly TheoryData<string> SecretProperties = new()
    {
        nameof(CipherMiniResponseModel.Data),
        nameof(CipherMiniResponseModel.Name),
        nameof(CipherMiniResponseModel.Notes),
        nameof(CipherMiniResponseModel.Login),
        nameof(CipherMiniResponseModel.Card),
        nameof(CipherMiniResponseModel.Identity),
        nameof(CipherMiniResponseModel.SecureNote),
        nameof(CipherMiniResponseModel.SSHKey),
        nameof(CipherMiniResponseModel.BankAccount),
        nameof(CipherMiniResponseModel.DriversLicense),
        nameof(CipherMiniResponseModel.Passport),
        nameof(CipherMiniResponseModel.Fields),
        nameof(CipherMiniResponseModel.PasswordHistory),
    };

    [Theory]
    [MemberData(nameof(SecretProperties))]
    public void SecretProperties_HaveNoPublicSetter(string propertyName)
    {
        var property = typeof(CipherMiniResponseModel).GetProperty(propertyName);

        Assert.NotNull(property);
        var setter = property!.SetMethod;
        Assert.True(setter is null || !setter.IsPublic,
            $"{propertyName} must not have a public setter — secret data may only be populated through the " +
            "witness-gated full-data path, never via a public constructor or object initializer.");
    }

    [Fact]
    public void FullCipherAccess_CannotBeMintedByPublicApi()
    {
        // The witness has no public constructor; only the leasing gate (in Bit.Core) mints one via the
        // internal factory methods. This keeps emitting full secret data a deliberate, gate-mediated act.
        var publicConstructors = typeof(FullCipherAccess)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.Empty(publicConstructors);

        var publicFactories = typeof(FullCipherAccess)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(FullCipherAccess));

        Assert.Empty(publicFactories);
    }

    [Fact]
    public void FullResponseTypes_DeriveFromTheirPartialCounterpart()
    {
        // The Full* types are the only producers of secret data and are siblings of the safe (partial)
        // types, so a list of the base type holds a polymorphic mix without a separate wire contract.
        Assert.True(typeof(CipherResponseModel).IsAssignableFrom(typeof(FullCipherResponseModel)));
        Assert.True(typeof(CipherDetailsResponseModel).IsAssignableFrom(typeof(FullCipherDetailsResponseModel)));
        Assert.True(typeof(CipherMiniResponseModel).IsAssignableFrom(typeof(FullCipherMiniResponseModel)));
        Assert.True(typeof(CipherMiniDetailsResponseModel).IsAssignableFrom(typeof(FullCipherMiniDetailsResponseModel)));
    }
}
