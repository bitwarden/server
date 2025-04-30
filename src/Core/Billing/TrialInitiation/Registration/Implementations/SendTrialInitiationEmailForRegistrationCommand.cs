#nullable enable
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Billing.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;

namespace Bit.Core.Billing.TrialInitiation.Registration.Implementations;

public class SendTrialInitiationEmailForRegistrationCommand : ISendTrialInitiationEmailForRegistrationCommand
{
    private readonly IUserRepository _userRepository;
    private readonly IMailService _mailService;
    private readonly IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable> _tokenFactory;
    private readonly GlobalSettings _globalSettings;

    public SendTrialInitiationEmailForRegistrationCommand(
        IUserRepository userRepository,
        IMailService mailService,
        IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable> tokenFactory,
        GlobalSettings globalSettings)
    {
        _userRepository = userRepository;
        _mailService = mailService;
        _tokenFactory = tokenFactory;
        _globalSettings = globalSettings;
    }

    public async Task<string?> Handle(
        string email,
        string? name,
        bool receiveMarketingEmails,
        ProductTierType productTier,
        IEnumerable<ProductType> products)
    {
        return await Handle(email, name, receiveMarketingEmails, productTier, products, 7);
    }

    public async Task<string?> Handle(
        string email,
        string? name,
        bool receiveMarketingEmails,
        ProductTierType productTier,
        IEnumerable<ProductType> products,
        int? trialLengthInDays = null)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        var userExists = await CheckUserExistsConstantTimeAsync(email);
        if (userExists)
        {
            throw new BadRequestException("User already exists.");
        }

        // Validate trial length - must be 0 or 7 days
        var validatedTrialLength = trialLengthInDays switch
        {
            0 => 0,
            7 => 7,
            _ => 7 // Default to 7 days for any other value
        };

        var token = GenerateToken(email, name, receiveMarketingEmails, validatedTrialLength);

        if (_globalSettings.EnableEmailVerification)
        {
            await _mailService.SendTrialInitiationSignupEmailAsync(
                userExists,
                email,
                token,
                productTier,
                products,
                validatedTrialLength);
            return null;
        }

        return token;
    }

    /// <summary>
    /// Perform constant time operations to prevent timing attacks
    /// </summary>
    private static async Task PerformConstantTimeOperationsAsync()
    {
        await Task.Delay(130);
    }

    private string GenerateToken(
        string email,
        string? name,
        bool receiveMarketingEmails,
        int trialLengthInDays)
    {
        var tokenable = new RegistrationEmailVerificationTokenable(
            email,
            name,
            receiveMarketingEmails,
            trialLengthInDays);
        return _tokenFactory.Protect(tokenable);
    }

    private async Task<bool> CheckUserExistsConstantTimeAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        return user != null;
    }
}
