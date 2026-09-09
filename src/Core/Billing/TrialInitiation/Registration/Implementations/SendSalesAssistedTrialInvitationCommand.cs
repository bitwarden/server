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
            Products = products,
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
}
