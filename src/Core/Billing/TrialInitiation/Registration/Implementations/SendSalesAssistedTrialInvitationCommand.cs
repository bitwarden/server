using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.Mail.Mailer;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Tokens;

namespace Bit.Core.Billing.TrialInitiation.Registration.Implementations;

public class SendSalesAssistedTrialInvitationCommand(
    IMailer mailer,
    IUserRepository userRepository,
    GlobalSettings globalSettings,
    IDataProtectorTokenFactory<SalesAssistedRegistrationTokenable> dataProtectorTokenFactory,
    ISalesAssistedRegistrationTokenableFactory tokenableFactory)
    : ISendSalesAssistedTrialInvitationCommand
{
    public async Task HandleAsync(
        string email,
        string? name,
        string senderEmail,
        ProductTierType productTier,
        IEnumerable<ProductType> products,
        int trialLength)
    {
        if (productTier == ProductTierType.TeamsStarter)
        {
            throw new BadRequestException("Teams Starter is no longer available for new trials.");
        }

        if (trialLength is < 1 or > 30)
        {
            throw new BadRequestException("Trial length must be between 1 and 30 days.");
        }

        var requestedProducts = products as IReadOnlyCollection<ProductType> ?? [.. products];

        if (productTier == ProductTierType.Families && requestedProducts.Contains(ProductType.SecretsManager))
        {
            throw new BadRequestException("Secrets Manager is not available for the Families plan.");
        }

        if (requestedProducts.Contains(ProductType.PrivilegedControls))
        {
            ValidatePrivilegedControlsConfiguration(productTier, requestedProducts);
        }

        if (requestedProducts.Contains(ProductType.PasswordManager) && requestedProducts.Contains(ProductType.SecretsManager))
        {
            throw new BadRequestException("Secrets Manager cannot be combined with Password Manager.");
        }

        var existingUser = await userRepository.GetByEmailAsync(email);
        if (existingUser != null)
        {
            throw new BadRequestException("A Bitwarden account already exists with this email address.");
        }

        var tokenable = tokenableFactory.CreateToken(email, name);
        var token = dataProtectorTokenFactory.Protect(tokenable);

        var view = new SalesAssistedTrialInvitationEmailView(globalSettings)
        {
            Token = token,
            Email = email,
            ProductTier = productTier,
            Products = requestedProducts,
            TrialLength = trialLength,
            SenderEmail = senderEmail,
            ExpiryDays = globalSettings.SalesAssistedRegistrationTokenLifetimeDays,
        };

        await mailer.SendEmail(new SalesAssistedTrialInvitationEmail
        {
            ToEmails = [email],
            View = view,
        });
    }

    private static void ValidatePrivilegedControlsConfiguration(
        ProductTierType productTier,
        IReadOnlyCollection<ProductType> products)
    {
        if (!products.Contains(ProductType.PasswordManager))
        {
            throw new BadRequestException("Privileged Controls requires Password Manager.");
        }

        if (productTier != ProductTierType.Enterprise)
        {
            throw new BadRequestException("Privileged Controls is only available on Password Manager Enterprise.");
        }

        if (products.Contains(ProductType.SecretsManager))
        {
            throw new BadRequestException("Privileged Controls cannot be combined with Secrets Manager.");
        }
    }
}
