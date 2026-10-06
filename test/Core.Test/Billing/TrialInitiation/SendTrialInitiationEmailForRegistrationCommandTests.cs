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
using Xunit;

namespace Bit.Core.Test.Billing.TrialInitiation;

[SutProviderCustomize]
public class SendTrialInitiationEmailForRegistrationCommandTests
{
    [Theory]
    [BitAutoData(ProductTierType.Teams)]
    [BitAutoData(ProductTierType.Families)]
    [BitAutoData(ProductTierType.Free)]
    [BitAutoData(ProductTierType.TeamsStarter)]
    public async Task Handle_PrivilegedControlsOnNonEnterpriseTier_Throws(
        ProductTierType productTier,
        string email,
        string name,
        SutProvider<SendTrialInitiationEmailForRegistrationCommand> sutProvider)
    {
        var products = new[] { ProductType.PasswordManager, ProductType.PrivilegedControls };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.Handle(email, name, false, productTier, products, 7));

        Assert.Equal("Privileged Controls is only available on Password Manager Enterprise.", exception.Message);
        await AssertNoEmailSent(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task Handle_PrivilegedControlsWithoutPasswordManager_Throws(
        string email,
        string name,
        SutProvider<SendTrialInitiationEmailForRegistrationCommand> sutProvider)
    {
        var products = new[] { ProductType.PrivilegedControls };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.Handle(email, name, false, ProductTierType.Enterprise, products, 7));

        Assert.Equal("Privileged Controls requires Password Manager.", exception.Message);
        await AssertNoEmailSent(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task Handle_PrivilegedControlsWithSecretsManager_Throws(
        string email,
        string name,
        SutProvider<SendTrialInitiationEmailForRegistrationCommand> sutProvider)
    {
        var products = new[] { ProductType.PasswordManager, ProductType.SecretsManager, ProductType.PrivilegedControls };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.Handle(email, name, false, ProductTierType.Enterprise, products, 7));

        Assert.Equal("Privileged Controls cannot be combined with Secrets Manager.", exception.Message);
        await AssertNoEmailSent(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task Handle_PasswordManagerOnly_SendsEmail(
        string email,
        string name,
        SutProvider<SendTrialInitiationEmailForRegistrationCommand> sutProvider)
    {
        await AssertSendsEmail(sutProvider, email, name, [ProductType.PasswordManager]);
    }

    [Theory]
    [BitAutoData]
    public async Task Handle_PasswordManagerAndSecretsManager_SendsEmail(
        string email,
        string name,
        SutProvider<SendTrialInitiationEmailForRegistrationCommand> sutProvider)
    {
        await AssertSendsEmail(sutProvider, email, name, [ProductType.PasswordManager, ProductType.SecretsManager]);
    }

    [Theory]
    [BitAutoData]
    public async Task Handle_InvalidConfiguration_ThrowsBeforeUserLookup(
        string email,
        string name,
        SutProvider<SendTrialInitiationEmailForRegistrationCommand> sutProvider)
    {
        var products = new[] { ProductType.PrivilegedControls };

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.Handle(email, name, false, ProductTierType.Enterprise, products, 7));

        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs().GetByEmailAsync(default!);
    }

    [Theory]
    [BitAutoData]
    public async Task Handle_EmptyEmail_ThrowsArgumentException(
        string name,
        SutProvider<SendTrialInitiationEmailForRegistrationCommand> sutProvider)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sutProvider.Sut.Handle("", name, false, ProductTierType.Enterprise, [ProductType.PasswordManager], 7));
    }

    [Theory]
    [BitAutoData]
    public async Task Handle_ProtectsTokenWithProvidedUserDetails(
        string email,
        string name,
        SutProvider<SendTrialInitiationEmailForRegistrationCommand> sutProvider)
    {
        sutProvider.GetDependency<Bit.Core.Settings.GlobalSettings>().EnableEmailVerification = true;
        sutProvider.GetDependency<IUserRepository>().GetByEmailAsync(email).Returns((User?)null);

        await sutProvider.Sut.Handle(email, name, true, ProductTierType.Enterprise, [ProductType.PasswordManager], 7);

        sutProvider.GetDependency<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>()
            .Received(1).Protect(Arg.Is<RegistrationEmailVerificationTokenable>(t =>
                t.Email == email && t.Name == name && t.ReceiveMarketingEmails));
    }

    [Theory]
    [BitAutoData]
    public async Task Handle_ExistingUser_SendsEmailFlaggedAsExistingUser(
        string email,
        string name,
        SutProvider<SendTrialInitiationEmailForRegistrationCommand> sutProvider)
    {
        sutProvider.GetDependency<Bit.Core.Settings.GlobalSettings>().EnableEmailVerification = true;
        sutProvider.GetDependency<IUserRepository>().GetByEmailAsync(email).Returns(new User { Email = email });
        sutProvider.GetDependency<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>()
            .Protect(Arg.Any<RegistrationEmailVerificationTokenable>())
            .Returns("protected-token");

        var result = await sutProvider.Sut.Handle(email, name, false, ProductTierType.Enterprise, [ProductType.PasswordManager], 7);

        Assert.Null(result);
        await sutProvider.GetDependency<IMailService>().Received(1).SendTrialInitiationSignupEmailAsync(
            true, email, "protected-token", ProductTierType.Enterprise,
            Arg.Any<IEnumerable<ProductType>>(), 7, false);
    }

    [Theory]
    [BitAutoData]
    public async Task Handle_EmailVerificationDisabled_NewUser_ReturnsTokenWithoutSendingEmail(
        string email,
        string name,
        SutProvider<SendTrialInitiationEmailForRegistrationCommand> sutProvider)
    {
        sutProvider.GetDependency<Bit.Core.Settings.GlobalSettings>().EnableEmailVerification = false;
        sutProvider.GetDependency<IUserRepository>().GetByEmailAsync(email).Returns((User?)null);
        sutProvider.GetDependency<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>()
            .Protect(Arg.Any<RegistrationEmailVerificationTokenable>())
            .Returns("protected-token");

        var result = await sutProvider.Sut.Handle(email, name, false, ProductTierType.Enterprise, [ProductType.PasswordManager], 7);

        Assert.Equal("protected-token", result);
        await AssertNoEmailSent(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task Handle_EmailVerificationDisabled_ExistingUser_Throws(
        string email,
        string name,
        SutProvider<SendTrialInitiationEmailForRegistrationCommand> sutProvider)
    {
        sutProvider.GetDependency<Bit.Core.Settings.GlobalSettings>().EnableEmailVerification = false;
        sutProvider.GetDependency<IUserRepository>().GetByEmailAsync(email).Returns(new User { Email = email });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.Handle(email, name, false, ProductTierType.Enterprise, [ProductType.PasswordManager], 7));

        Assert.Equal($"Email {email} is already taken", exception.Message);
        await AssertNoEmailSent(sutProvider);
    }

    private static async Task AssertSendsEmail(
        SutProvider<SendTrialInitiationEmailForRegistrationCommand> sutProvider,
        string email,
        string name,
        ProductType[] products)
    {
        sutProvider.GetDependency<Bit.Core.Settings.GlobalSettings>().EnableEmailVerification = true;
        sutProvider.GetDependency<IUserRepository>().GetByEmailAsync(email).Returns((User?)null);
        sutProvider.GetDependency<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>()
            .Protect(Arg.Any<RegistrationEmailVerificationTokenable>())
            .Returns("protected-token");

        var result = await sutProvider.Sut.Handle(email, name, false, ProductTierType.Enterprise, products, 7);

        Assert.Null(result);
        await sutProvider.GetDependency<IMailService>().Received(1).SendTrialInitiationSignupEmailAsync(
            false, email, "protected-token", ProductTierType.Enterprise,
            Arg.Any<IEnumerable<ProductType>>(), 7, false);
    }

    private static async Task AssertNoEmailSent(SutProvider<SendTrialInitiationEmailForRegistrationCommand> sutProvider)
    {
        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs().SendTrialInitiationSignupEmailAsync(
            default, default!, default!, default, default!, default, default);
    }
}
