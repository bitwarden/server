using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Queries.Interfaces;

#nullable enable

namespace Bit.Core.Tools.SendFeatures.Queries;

/// <inheritdoc cref="ISendAuthenticationQuery"/>
public class SendAuthenticationQuery : ISendAuthenticationQuery
{
    private static readonly NotAuthenticated NOT_AUTHENTICATED = new NotAuthenticated();
    private static readonly NeverAuthenticate NEVER_AUTHENTICATE = new NeverAuthenticate();

    private readonly ISendRepository _sendRepository;

    /// <summary>
    /// Instantiates the command
    /// </summary>
    /// <param name="sendRepository">
    /// Retrieves send records
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sendRepository"/> is <see langword="null"/>.
    /// </exception>
    public SendAuthenticationQuery(ISendRepository sendRepository)
    {
        _sendRepository = sendRepository ?? throw new ArgumentNullException(nameof(sendRepository));
    }

    /// <inheritdoc cref="ISendAuthenticationQuery.GetAuthenticationMethod"/>
    public async Task<SendAuthenticationMethod> GetAuthenticationMethod(Guid sendId)
    {
        var send = await _sendRepository.GetByIdAsync(sendId);

        SendAuthenticationMethod method = send switch
        {
            null => NEVER_AUTHENTICATE,
            var s when s.AccessCount >= s.MaxAccessCount => NEVER_AUTHENTICATE,
            var s when s.Emails is not null => emailOtp(s.Emails),
            var s when s.Password is not null => new ResourcePassword(s.Password),
            _ => NOT_AUTHENTICATED
        };

        return method;
    }

    private EmailOtp emailOtp(string emails)
    {
        var list = emails.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new EmailOtp(list);
    }
}
