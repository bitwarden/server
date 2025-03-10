#nullable enable
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;

namespace Bit.Core.Auth.UserFeatures.Registration.Implementations;

/// <summary>
/// If email verification is enabled, this command will send a verification email to the user which will
///  contain a link to complete the registration process.
/// If email verification is disabled, this command will return a token that can be used to complete the registration process directly.
/// </summary>
public class SendVerificationEmailForRegistrationCommand : ISendVerificationEmailForRegistrationCommand
{

    private readonly IUserRepository _userRepository;
    private readonly GlobalSettings _globalSettings;
    private readonly IMailService _mailService;
    private readonly IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable> _tokenDataFactory;
    private readonly IFeatureService _featureService;

    public SendVerificationEmailForRegistrationCommand(
        IUserRepository userRepository,
        GlobalSettings globalSettings,
        IMailService mailService,
        IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable> tokenDataFactory,
        IFeatureService featureService)
    {
        _userRepository = userRepository;
        _globalSettings = globalSettings;
        _mailService = mailService;
        _tokenDataFactory = tokenDataFactory;
        _featureService = featureService;

    }

    public async Task<string?> Run(string email, string? name, bool receiveMarketingEmails)
    {
        if (_globalSettings.DisableUserRegistration)
        {
            throw new BadRequestException("Open registration has been disabled by the system administrator.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentNullException(nameof(email));
        }

        // Check to see if the user already exists
        var user = await _userRepository.GetByEmailAsync(email);
        var userExists = user != null;

        // Delays enabled by default; flag must be enabled to remove the delays.
        var delaysEnabled = !_featureService.IsEnabled(FeatureFlagKeys.EmailVerificationDisableTimingDelays);

        if (!_globalSettings.EnableEmailVerification)
        {

            if (userExists)
            {

                if (delaysEnabled)
                {
                    // Add delay to prevent timing attacks
                    // Note: sub 140 ms feels responsive to users so we are using a random value between 100 - 130 ms
                    // as it should be long enough to prevent timing attacks but not too long to be noticeable to the user.
                    await Task.Delay(Random.Shared.Next(100, 130));
                }

                throw new BadRequestException($"Email {email} is already taken");
            }

            // if user doesn't exist, return a EmailVerificationTokenable in the response body.
            var token = GenerateToken(email, name, receiveMarketingEmails);

            return token;
        }

        if (!userExists)
        {
            // If the user doesn't exist, create a new EmailVerificationTokenable and send the user
            // an email with a link to verify their email address
            var token = GenerateToken(email, name, receiveMarketingEmails);
            await _mailService.SendRegistrationVerificationEmailAsync(email, token);
        }

        if (delaysEnabled)
        {
            // Add random delay between 100ms-130ms to prevent timing attacks
            await Task.Delay(Random.Shared.Next(100, 130));
        }
        // User exists but we will return a 200 regardless of whether the email was sent or not; so return null
        return null;
    }

    private string GenerateToken(string email, string? name, bool receiveMarketingEmails)
    {
        var registrationEmailVerificationTokenable = new RegistrationEmailVerificationTokenable(email, name, receiveMarketingEmails);
        return _tokenDataFactory.Protect(registrationEmailVerificationTokenable);
    }
}

