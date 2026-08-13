using System.Globalization;
using System.Net;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Repositories;
using Bit.Core.Tokens;

namespace Bit.Seeder.Queries;

public class UserEmailVerificationQuery(IUserRepository userRepository,
    IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable> dataProtectorTokenizer) : IQuery<UserEmailVerificationQuery.Request, UserEmailVerificationQuery.Response>
{
    public class Request
    {
        public string? Name { get; set; } = null;
        public required string Email { get; set; }
        public string? FromMarketing { get; set; } = null;
        public bool ReceiveMarketingEmails { get; set; } = false;
    }

    public class Response
    {
        public required string Url { get; set; }
        public required bool EmailVerified { get; set; }
    }

    public async Task<Response> Execute(Request request)
    {
        var user = await userRepository.GetByEmailAsync(request.Email);

        var token = generateToken(request.Email, request.Name, request.ReceiveMarketingEmails);

        return new()
        {
            Url = Url(token, request.Email, request.FromMarketing),
            EmailVerified = user?.EmailVerified ?? false
        };
    }

    private string Url(string token, string email, string? fromMarketing = null)
    {
        return string.Format(CultureInfo.InvariantCulture, "/redirect-connector.html#finish-signup?token={0}&email={1}&fromEmail=true{2}",
            WebUtility.UrlEncode(token),
            WebUtility.UrlEncode(email),
            !string.IsNullOrEmpty(fromMarketing) ? $"&fromMarketing={fromMarketing}" : string.Empty);
    }

    private string generateToken(string email, string? name, bool receiveMarketingEmails)
    {

        return dataProtectorTokenizer.Protect(
            new RegistrationEmailVerificationTokenable(email, name, receiveMarketingEmails)
        );
    }
}
