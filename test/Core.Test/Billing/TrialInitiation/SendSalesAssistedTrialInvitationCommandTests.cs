using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.Mail.Mailer;
using Bit.Core.Billing.TrialInitiation.Registration.Implementations;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.TrialInitiation;

[SutProviderCustomize]
public class SendSalesAssistedTrialInvitationCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task HandleAsync_NewUser_MintsTokenProtectsAndSendsEmail(
        string email,
        string name,
        string senderEmail,
        SutProvider<SendSalesAssistedTrialInvitationCommand> sutProvider)
    {
        // Arrange
        var products = new[] { ProductType.PasswordManager };
        const ProductTierType productTier = ProductTierType.Enterprise;
        const int trialLength = 7;
        const string protectedToken = "protected-token";

        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(email)
            .Returns((User?)null);

        var tokenable = new SalesAssistedRegistrationTokenable { Email = email, Name = name };
        sutProvider.GetDependency<ISalesAssistedRegistrationTokenableFactory>()
            .CreateToken(email, name)
            .Returns(tokenable);

        sutProvider.GetDependency<IDataProtectorTokenFactory<SalesAssistedRegistrationTokenable>>()
            .Protect(tokenable)
            .Returns(protectedToken);

        // Act
        await sutProvider.Sut.HandleAsync(email, name, senderEmail, productTier, products, trialLength, false);

        // Assert
        sutProvider.GetDependency<ISalesAssistedRegistrationTokenableFactory>()
            .Received(1)
            .CreateToken(email, name);
        sutProvider.GetDependency<IDataProtectorTokenFactory<SalesAssistedRegistrationTokenable>>()
            .Received(1)
            .Protect(tokenable);

        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<SalesAssistedTrialInvitationEmail>(mail =>
                mail.ToEmails.SequenceEqual(new[] { email }) &&
                mail.View.Email == email &&
                mail.View.IsExistingUser == false &&
                mail.View.Token == protectedToken &&
                mail.View.ProductTier == productTier &&
                mail.View.Products.SequenceEqual(products) &&
                mail.View.TrialLength == trialLength &&
                mail.View.PaymentOptional == false &&
                mail.View.SenderEmail == senderEmail));
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAsync_ExistingUser_ThrowsBadRequest(
        string name,
        string senderEmail,
        User existingUser,
        SutProvider<SendSalesAssistedTrialInvitationCommand> sutProvider)
    {
        // Arrange
        var products = new[] { ProductType.PasswordManager };

        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(existingUser.Email)
            .Returns(existingUser);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.HandleAsync(existingUser.Email, name, senderEmail, ProductTierType.Enterprise, products, 7, false));

        sutProvider.GetDependency<ISalesAssistedRegistrationTokenableFactory>()
            .DidNotReceiveWithAnyArgs()
            .CreateToken(default, default);

        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs()
            .SendEmail(Arg.Any<SalesAssistedTrialInvitationEmail>());
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAsync_SenderEmail_ForwardedIntoView(
        string email,
        string name,
        string senderEmail,
        SutProvider<SendSalesAssistedTrialInvitationCommand> sutProvider)
    {
        // Arrange
        var products = new[] { ProductType.PasswordManager };
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(email)
            .Returns((User?)null);
        sutProvider.GetDependency<ISalesAssistedRegistrationTokenableFactory>()
            .CreateToken(email, name)
            .Returns(new SalesAssistedRegistrationTokenable { Email = email, Name = name });

        // Act
        await sutProvider.Sut.HandleAsync(email, name, senderEmail, ProductTierType.Enterprise, products, 7, false);

        // Assert
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<SalesAssistedTrialInvitationEmail>(mail =>
                mail.View.SenderEmail == senderEmail));
    }

    [Theory]
    [BitAutoData(-1)]
    [BitAutoData(31)]
    public async Task HandleAsync_TrialLengthOutOfRange_ThrowsBadRequest(
        int trialLength,
        string email,
        string name,
        string senderEmail,
        SutProvider<SendSalesAssistedTrialInvitationCommand> sutProvider)
    {
        // Arrange
        var products = new[] { ProductType.PasswordManager };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.HandleAsync(email, name, senderEmail, ProductTierType.Enterprise, products, trialLength, false));

        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs()
            .SendEmail(Arg.Any<SalesAssistedTrialInvitationEmail>());
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAsync_PaymentOptionalWithZeroTrialLength_ThrowsBadRequest(
        string email,
        string name,
        string senderEmail,
        SutProvider<SendSalesAssistedTrialInvitationCommand> sutProvider)
    {
        // Arrange
        var products = new[] { ProductType.PasswordManager };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.HandleAsync(email, name, senderEmail, ProductTierType.Enterprise, products, 0, true));
    }
}
