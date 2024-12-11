using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.UserFeatures.Registration.Implementations;
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
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Core.Test.Auth.UserFeatures.Registration;

[SutProviderCustomize]
public class SendVerificationEmailForRegistrationCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task SendVerificationEmailForRegistrationCommand_WhenIsNewUserAndEnableEmailVerificationTrue_SendsEmailAndReturnsNull(
        SutProvider<SendVerificationEmailForRegistrationCommand> sutProvider,
        string email,
        string name,
        bool receiveMarketingEmails
    )
    {
        // Arrange
        sutProvider.GetDependency<IUserRepository>().GetByEmailAsync(email).ReturnsNull();

        sutProvider.GetDependency<GlobalSettings>().EnableEmailVerification = true;

        sutProvider.GetDependency<GlobalSettings>().DisableUserRegistration = false;

        sutProvider
            .GetDependency<IMailService>()
            .SendRegistrationVerificationEmailAsync(email, Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var mockedToken = "token";
        sutProvider
            .GetDependency<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>()
            .Protect(Arg.Any<RegistrationEmailVerificationTokenable>())
            .Returns(mockedToken);

        // Act
        var result = await sutProvider.Sut.Run(email, name, receiveMarketingEmails);

        // Assert
        await sutProvider
            .GetDependency<IMailService>()
            .Received(1)
            .SendRegistrationVerificationEmailAsync(email, mockedToken);
        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public async Task SendVerificationEmailForRegistrationCommand_WhenIsExistingUserAndEnableEmailVerificationTrue_ReturnsNull(
        SutProvider<SendVerificationEmailForRegistrationCommand> sutProvider,
        string email,
        string name,
        bool receiveMarketingEmails
    )
    {
        // Arrange
        sutProvider.GetDependency<IUserRepository>().GetByEmailAsync(email).Returns(new User());

        sutProvider.GetDependency<GlobalSettings>().EnableEmailVerification = true;

        sutProvider.GetDependency<GlobalSettings>().DisableUserRegistration = false;

        var mockedToken = "token";
        sutProvider
            .GetDependency<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>()
            .Protect(Arg.Any<RegistrationEmailVerificationTokenable>())
            .Returns(mockedToken);

        // Act
        var result = await sutProvider.Sut.Run(email, name, receiveMarketingEmails);

        // Assert
        await sutProvider
            .GetDependency<IMailService>()
            .DidNotReceive()
            .SendRegistrationVerificationEmailAsync(email, mockedToken);
        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public async Task SendVerificationEmailForRegistrationCommand_WhenIsNewUserAndEnableEmailVerificationFalse_ReturnsToken(
        SutProvider<SendVerificationEmailForRegistrationCommand> sutProvider,
        string email,
        string name,
        bool receiveMarketingEmails
    )
    {
        // Arrange
        sutProvider.GetDependency<IUserRepository>().GetByEmailAsync(email).ReturnsNull();

        sutProvider.GetDependency<GlobalSettings>().EnableEmailVerification = false;

        sutProvider.GetDependency<GlobalSettings>().DisableUserRegistration = false;

        var mockedToken = "token";
        sutProvider
            .GetDependency<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>()
            .Protect(Arg.Any<RegistrationEmailVerificationTokenable>())
            .Returns(mockedToken);

        // Act
        var result = await sutProvider.Sut.Run(email, name, receiveMarketingEmails);

        // Assert
        Assert.Equal(mockedToken, result);
    }

    [Theory]
    [BitAutoData]
    public async Task SendVerificationEmailForRegistrationCommand_WhenOpenRegistrationDisabled_ThrowsBadRequestException(
        SutProvider<SendVerificationEmailForRegistrationCommand> sutProvider,
        string email,
        string name,
        bool receiveMarketingEmails
    )
    {
        // Arrange
        sutProvider.GetDependency<GlobalSettings>().DisableUserRegistration = true;

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.Run(email, name, receiveMarketingEmails)
        );
    }

    [Theory]
    [BitAutoData]
    public async Task SendVerificationEmailForRegistrationCommand_WhenIsExistingUserAndEnableEmailVerificationFalse_ThrowsBadRequestException(
        SutProvider<SendVerificationEmailForRegistrationCommand> sutProvider,
        string email,
        string name,
        bool receiveMarketingEmails
    )
    {
        // Arrange
        sutProvider.GetDependency<IUserRepository>().GetByEmailAsync(email).Returns(new User());

        sutProvider.GetDependency<GlobalSettings>().EnableEmailVerification = false;

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.Run(email, name, receiveMarketingEmails)
        );
    }

    [Theory]
    [BitAutoData]
    public async Task SendVerificationEmailForRegistrationCommand_WhenNullEmail_ThrowsArgumentNullException(
        SutProvider<SendVerificationEmailForRegistrationCommand> sutProvider,
        string name,
        bool receiveMarketingEmails
    )
    {
        sutProvider.GetDependency<GlobalSettings>().DisableUserRegistration = false;

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sutProvider.Sut.Run(null, name, receiveMarketingEmails)
        );
    }

    [Theory]
    [BitAutoData]
    public async Task SendVerificationEmailForRegistrationCommand_WhenEmptyEmail_ThrowsArgumentNullException(
        SutProvider<SendVerificationEmailForRegistrationCommand> sutProvider,
        string name,
        bool receiveMarketingEmails
    )
    {
        sutProvider.GetDependency<GlobalSettings>().DisableUserRegistration = false;
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sutProvider.Sut.Run("", name, receiveMarketingEmails)
        );
    }
}
