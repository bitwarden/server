using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.TrialInitiation.Registration.Implementations;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.Billing.TrialInitiation.Registration;

[SutProviderCustomize]
public class SendTrialInitiationEmailForRegistrationCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task Handle_WhenTrialLengthIs7Days_SendsEmailWithCorrectTrialLength(
        SutProvider<SendTrialInitiationEmailForRegistrationCommand> sutProvider,
        string email,
        string name,
        bool receiveMarketingEmails,
        ProductTierType productTier,
        IEnumerable<ProductType> products)
    {
        // Arrange
        sutProvider.GetDependency<Bit.Core.Settings.GlobalSettings>()
            .EnableEmailVerification = true;

        var userExists = false;
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(email)
            .ReturnsNull();

        var mockedToken = "token";
        sutProvider.GetDependency<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>()
            .Protect(Arg.Any<RegistrationEmailVerificationTokenable>())
            .Returns(mockedToken);

        // Act
        var result = await sutProvider.Sut.Handle(email, name, receiveMarketingEmails, productTier, products, 7);

        // Assert
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendTrialInitiationSignupEmailAsync(userExists, email, mockedToken, productTier, products, 7);
        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public async Task Handle_WhenTrialLengthIs0Days_SendsEmailWithCorrectTrialLength(
        SutProvider<SendTrialInitiationEmailForRegistrationCommand> sutProvider,
        string email,
        string name,
        bool receiveMarketingEmails,
        ProductTierType productTier,
        IEnumerable<ProductType> products)
    {
        // Arrange
        sutProvider.GetDependency<Bit.Core.Settings.GlobalSettings>()
            .EnableEmailVerification = true;

        var userExists = false;
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(email)
            .ReturnsNull();

        var mockedToken = "token";
        sutProvider.GetDependency<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>()
            .Protect(Arg.Any<RegistrationEmailVerificationTokenable>())
            .Returns(mockedToken);

        // Act
        var result = await sutProvider.Sut.Handle(email, name, receiveMarketingEmails, productTier, products, 0);

        // Assert
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendTrialInitiationSignupEmailAsync(userExists, email, mockedToken, productTier, products, 0);
        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public async Task Handle_WhenTrialLengthIsInvalid_DefaultsTo7Days(
        SutProvider<SendTrialInitiationEmailForRegistrationCommand> sutProvider,
        string email,
        string name,
        bool receiveMarketingEmails,
        ProductTierType productTier,
        IEnumerable<ProductType> products)
    {
        // Arrange
        sutProvider.GetDependency<Bit.Core.Settings.GlobalSettings>()
            .EnableEmailVerification = true;

        var userExists = false;
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(email)
            .ReturnsNull();

        var mockedToken = "token";
        sutProvider.GetDependency<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>()
            .Protect(Arg.Any<RegistrationEmailVerificationTokenable>())
            .Returns(mockedToken);

        // Act
        var result = await sutProvider.Sut.Handle(email, name, receiveMarketingEmails, productTier, products, 5); // Invalid trial length

        // Assert
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendTrialInitiationSignupEmailAsync(userExists, email, mockedToken, productTier, products, 7); // Should default to 7
        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public async Task Handle_WhenTrialLengthIsNull_DefaultsTo7Days(
        SutProvider<SendTrialInitiationEmailForRegistrationCommand> sutProvider,
        string email,
        string name,
        bool receiveMarketingEmails,
        ProductTierType productTier,
        IEnumerable<ProductType> products)
    {
        // Arrange
        sutProvider.GetDependency<Bit.Core.Settings.GlobalSettings>()
            .EnableEmailVerification = true;

        var userExists = false;
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(email)
            .ReturnsNull();

        var mockedToken = "token";
        sutProvider.GetDependency<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>()
            .Protect(Arg.Any<RegistrationEmailVerificationTokenable>())
            .Returns(mockedToken);

        // Act
        var result = await sutProvider.Sut.Handle(email, name, receiveMarketingEmails, productTier, products); // No trial length specified

        // Assert
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendTrialInitiationSignupEmailAsync(userExists, email, mockedToken, productTier, products, 7); // Should default to 7
        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public async Task Handle_WhenEmailVerificationDisabled_ReturnsToken(
        SutProvider<SendTrialInitiationEmailForRegistrationCommand> sutProvider,
        string email,
        string name,
        bool receiveMarketingEmails,
        ProductTierType productTier,
        IEnumerable<ProductType> products)
    {
        // Arrange
        sutProvider.GetDependency<Bit.Core.Settings.GlobalSettings>()
            .EnableEmailVerification = false;

        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(email)
            .ReturnsNull();

        var mockedToken = "token";
        sutProvider.GetDependency<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>()
            .Protect(Arg.Any<RegistrationEmailVerificationTokenable>())
            .Returns(mockedToken);

        // Act
        var result = await sutProvider.Sut.Handle(email, name, receiveMarketingEmails, productTier, products, 7);

        // Assert
        await sutProvider.GetDependency<IMailService>()
            .DidNotReceive()
            .SendTrialInitiationSignupEmailAsync(Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<ProductTierType>(), Arg.Any<IEnumerable<ProductType>>(), Arg.Any<int>());
        Assert.Equal(mockedToken, result);
    }

    [Theory]
    [BitAutoData]
    public async Task Handle_WhenUserExists_ThrowsBadRequestException(
        SutProvider<SendTrialInitiationEmailForRegistrationCommand> sutProvider,
        string email,
        string name,
        bool receiveMarketingEmails,
        ProductTierType productTier,
        IEnumerable<ProductType> products)
    {
        // Arrange
        sutProvider.GetDependency<Bit.Core.Settings.GlobalSettings>()
            .EnableEmailVerification = false;

        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(email)
            .Returns(new User());

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.Handle(email, name, receiveMarketingEmails, productTier, products, 7));
    }

    [Theory]
    [BitAutoData]
    public async Task Handle_WhenEmailIsEmpty_ThrowsArgumentException(
        SutProvider<SendTrialInitiationEmailForRegistrationCommand> sutProvider,
        string name,
        bool receiveMarketingEmails,
        ProductTierType productTier,
        IEnumerable<ProductType> products)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sutProvider.Sut.Handle("", name, receiveMarketingEmails, productTier, products, 7));
    }
}
