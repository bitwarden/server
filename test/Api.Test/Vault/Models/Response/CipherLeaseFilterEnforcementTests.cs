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
    /// Each <c>Full*</c> model must derive from its partial counterpart, so a list typed to the partial
    /// type can hold a polymorphic mix and the wire contract stays unchanged.
    /// </summary>
    [Theory]
    [InlineData(typeof(CipherMiniResponseModel), typeof(FullCipherMiniResponseModel))]
    [InlineData(typeof(CipherResponseModel), typeof(FullCipherResponseModel))]
    [InlineData(typeof(CipherDetailsResponseModel), typeof(FullCipherDetailsResponseModel))]
    [InlineData(typeof(CipherMiniDetailsResponseModel), typeof(FullCipherMiniDetailsResponseModel))]
    public void FullModel_DerivesFromItsPartialCounterpart(Type partialType, Type fullType)
    {
        Assert.True(
            partialType.IsAssignableFrom(fullType),
            $"{fullType.Name} must derive from {partialType.Name}.");
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
}
