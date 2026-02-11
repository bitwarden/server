using System.Globalization;
using Bit.Core.Repositories;

namespace Bit.Seeder.Queries;

public class UserEmailVerificationQuery(IUserRepository userRepository) : IQuery<UserEmailVerificationQuery.Request, UserEmailVerificationQuery.Response>
{
    public class Request
    {
        public required string Email { get; set; }
        public string FromMarketing { get; set; } = string.Empty;
    }

    public class Response
    {
        public required string Url { get; set; }
        public required bool EmailVerified { get; set; }
    }

    public async Task<Response> Execute(Request request)
    {
        var user = await userRepository.GetByEmailAsync(request.Email);

        return new()
        {
            Url = Url(string.Empty, request.Email, request.FromMarketing),
            EmailVerified = user?.EmailVerified ?? false
        };
    }

    private string Url(string token, string email, string? fromMarketing = null)
    {
        return string.Format(CultureInfo.InvariantCulture, "/redirect-connector.html#finish-signup?token={0}&email={1}&fromEmail=true{2}",
            token,
            email,
            !string.IsNullOrEmpty(fromMarketing) ? $"&fromMarketing={fromMarketing}" : string.Empty);
    }
}
