using System.Reflection;
using Bit.Api.Vault.Models.Response;
using Bit.Core.Vault.Authorization;
using Xunit;

namespace Bit.Api.Test.Vault.Models.Response;

/// <summary>
/// Fitness tests guarding the structural invariants that make PAM credential-leasing filtering
/// fail closed. These assert on shape rather than behaviour, so a future refactor that reopens one
/// of these holes fails here rather than silently leaking secret cipher data.
/// </summary>
public class CipherLeaseFilterEnforcementTests
{
    /// <summary>
    /// Every property on the cipher response models that carries secret cipher data. These may only be
    /// written through the witness-gated <c>PopulateFullData</c> path, so none may have a public setter.
    /// </summary>
    [Theory]
    [InlineData(nameof(CipherMiniResponseModel.Data))]
    [InlineData(nameof(CipherMiniResponseModel.Name))]
    [InlineData(nameof(CipherMiniResponseModel.Notes))]
    [InlineData(nameof(CipherMiniResponseModel.Login))]
    [InlineData(nameof(CipherMiniResponseModel.Card))]
    [InlineData(nameof(CipherMiniResponseModel.Identity))]
    [InlineData(nameof(CipherMiniResponseModel.SecureNote))]
    [InlineData(nameof(CipherMiniResponseModel.SSHKey))]
    [InlineData(nameof(CipherMiniResponseModel.BankAccount))]
    [InlineData(nameof(CipherMiniResponseModel.DriversLicense))]
    [InlineData(nameof(CipherMiniResponseModel.Passport))]
    [InlineData(nameof(CipherMiniResponseModel.Fields))]
    [InlineData(nameof(CipherMiniResponseModel.PasswordHistory))]
    [InlineData(nameof(CipherMiniResponseModel.Attachments))]
    public void SecretProperties_HaveNoPublicSetter(string propertyName)
    {
        var property = typeof(CipherMiniResponseModel).GetProperty(propertyName);

        Assert.NotNull(property);
        Assert.True(
            property.SetMethod is null || !property.SetMethod.IsPublic,
            $"{propertyName} must not have a public setter: secret cipher data may only be written " +
            "through the FullCipherAccess-gated PopulateFullData path.");
    }

    [Fact]
    public void FullCipherAccess_CannotBeMintedByApplicationCode()
    {
        var type = typeof(FullCipherAccess);

        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(type
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(FullCipherAccess)));
    }

    /// <summary>
    /// Each shared base must stay abstract. A concrete base would let a call site emit a response without
    /// stating whether it carries secret data — the silent default this design exists to remove.
    /// </summary>
    [Theory]
    [InlineData(typeof(CipherMiniResponseModel))]
    [InlineData(typeof(CipherResponseModel))]
    [InlineData(typeof(CipherDetailsResponseModel))]
    [InlineData(typeof(CipherMiniDetailsResponseModel))]
    public void BaseModel_IsAbstract(Type baseType)
    {
        Assert.True(
            baseType.IsAbstract,
            $"{baseType.Name} must stay abstract so every call site picks the partial or the full shape.");
    }

    /// <summary>
    /// Every constructible cipher response must be a <c>Partial*</c> or a <c>Full*</c>, and sealed, so the
    /// shape a response carries is legible from its type and cannot be extended into a third meaning.
    /// A list typed to the shared base still holds a polymorphic mix of the two.
    /// </summary>
    [Fact]
    public void ConcreteModels_AreSealedAndNameTheirShape()
    {
        var concreteModels = typeof(CipherMiniResponseModel).Assembly
            .GetTypes()
            .Where(t => typeof(CipherMiniResponseModel).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(concreteModels);
        Assert.All(concreteModels, type =>
        {
            Assert.True(
                type.Name.StartsWith("Partial") || type.Name.StartsWith("Full"),
                $"{type.Name} must be named Partial* or Full*: a cipher response has to say whether it " +
                "carries the cipher's secret data.");
            Assert.True(type.IsSealed, $"{type.Name} must be sealed.");
        });
    }

    /// <summary>
    /// Every public constructor of a <c>Full*</c> model must take a <see cref="FullCipherAccess"/>, so
    /// full secret data cannot be emitted without one.
    /// </summary>
    [Theory]
    [InlineData(typeof(FullCipherMiniResponseModel))]
    [InlineData(typeof(FullCipherResponseModel))]
    [InlineData(typeof(FullCipherDetailsResponseModel))]
    [InlineData(typeof(FullCipherMiniDetailsResponseModel))]
    public void FullModel_EveryPublicConstructor_RequiresAWitness(Type fullType)
    {
        var constructors = fullType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.NotEmpty(constructors);
        Assert.All(constructors, ctor =>
            Assert.Contains(ctor.GetParameters(), p => p.ParameterType == typeof(FullCipherAccess)));
    }

    /// <summary>
    /// No <c>Partial*</c> model may accept a witness. Taking one would suggest it can emit full data, and
    /// the only way to emit full data must remain the <c>Full*</c> types.
    /// </summary>
    [Theory]
    [InlineData(typeof(PartialCipherMiniResponseModel))]
    [InlineData(typeof(PartialCipherResponseModel))]
    [InlineData(typeof(PartialCipherDetailsResponseModel))]
    public void PartialModel_NoConstructor_TakesAWitness(Type partialType)
    {
        var constructors = partialType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.NotEmpty(constructors);
        Assert.All(constructors, ctor =>
            Assert.DoesNotContain(ctor.GetParameters(), p => p.ParameterType == typeof(FullCipherAccess)));
    }
}
