#nullable enable
namespace Bit.Core.Auth.UserFeatures.Registration;

public interface ISendVerificationEmailForRegistrationCommand
{
    public Task<string?> Run(string email, string? name, bool receiveMarketingEmails);
}
