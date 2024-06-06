using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;

namespace Bit.Core.Auth.UserFeatures.Registration.Implementations;

public class SendVerificationEmailForRegistrationCommand(
    IUserRepository userRepository,
    GlobalSettings globalSettings,
    IMailService mailService,
    IDataProtectorTokenFactory<EmailVerificationTokenable> tokenDataFactory)
    : ISendVerificationEmailForRegistrationCommand
{

    public async Task<string> Run(string email, string name, bool receiveMarketingEmails)
    {
        // Check to see if the user already exists
        var user = await userRepository.GetByEmailAsync(email);
        var userExists = user != null;

        if (!globalSettings.EnableEmailVerification && userExists)
        {
            // Add delay to prevent timing attacks
            await Task.Delay(2000);
            throw new BadRequestException($"Email {email} is already taken");
        }

        if (!globalSettings.EnableEmailVerification && !userExists)
        {
            // if email doesn't exist, return a EmailVerificationTokenable in the response body.
            var emailVerificationTokenable = new EmailVerificationTokenable(email, name, receiveMarketingEmails);
            var emailVerificationToken = tokenDataFactory.Protect(emailVerificationTokenable);

            return emailVerificationToken;
        }

        // GlobalSettings.EnableEmailVerification is true

        if (!userExists)
        {
            // If the user doesn't exist, create a new EmailVerificationTokenable and send the user
            // an email with a link to verify their email address

            var emailVerificationTokenable = new EmailVerificationTokenable(email, name, receiveMarketingEmails);
            var emailVerificationToken = tokenDataFactory.Protect(emailVerificationTokenable);

            // TODO: send email
        }

        // User exists but we will return a 200 regardless of whether the email was sent or not; so return null
        return null;
    }
}

