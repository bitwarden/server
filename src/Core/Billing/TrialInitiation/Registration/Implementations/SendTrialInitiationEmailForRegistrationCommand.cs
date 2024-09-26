#nullable enable
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Billing.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Core.Utilities;

namespace Bit.Core.Billing.TrialInitiation.Registration.Implementations;

public class SendTrialInitiationEmailForRegistrationCommand(
    IUserRepository userRepository,
    GlobalSettings globalSettings,
    IMailService mailService,
    IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable> tokenDataFactory)
    : ISendTrialInitiationEmailForRegistrationCommand
{
    public async Task<string?> Handle(
        string email,
        string? name,
        bool receiveMarketingEmails,
        ProductTierType productTier,
        IEnumerable<ProductType> products)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email, nameof(email));

        var userExists = await CheckUserExistsConstantTimeAsync(email);
        var token = GenerateToken(email, name, receiveMarketingEmails);

        if (!globalSettings.EnableEmailVerification)
        {
            await PerformConstantTimeOperationsAsync();

            if (userExists)
            {
                throw new BadRequestException($"Email {email} is already taken");
            }

            return token;
        }

        await PerformConstantTimeOperationsAsync();

        if (!userExists)
        {
            await mailService.SendTrialInitiationSignupEmailAsync(email, token, productTier, products);
        }

        return null;
    }

    /// <summary>
    /// Perform constant time operations to prevent timing attacks
    /// </summary>
    private static async Task PerformConstantTimeOperationsAsync()
    {
        await Task.Delay(130);
    }

    private string GenerateToken(string email, string? name, bool receiveMarketingEmails)
    {
        var tokenable = new RegistrationEmailVerificationTokenable(email, name, receiveMarketingEmails);
        return tokenDataFactory.Protect(tokenable);
    }

    private async Task<bool> CheckUserExistsConstantTimeAsync(string email)
    {
        var user = await userRepository.GetByEmailAsync(email);

        return CoreHelpers.FixedTimeEquals(user?.Email.ToLowerInvariant() ?? string.Empty, email.ToLowerInvariant());
    }
}
