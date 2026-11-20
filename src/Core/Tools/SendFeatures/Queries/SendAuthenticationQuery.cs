using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Queries.Interfaces;
using Bit.Core.Vault.Repositories;

#nullable enable

namespace Bit.Core.Tools.SendFeatures.Queries;

/// <inheritdoc cref="ISendAuthenticationQuery"/>
public class SendAuthenticationQuery : ISendAuthenticationQuery
{
    private static readonly NotAuthenticated NOT_AUTHENTICATED = new NotAuthenticated();
    private static readonly SendInaccessible SEND_INACCESSIBLE = new SendInaccessible();

    private readonly ISendRepository _sendRepository;
    private readonly ICipherRepository _cipherRepository;

    /// <summary>
    /// Instantiates the command
    /// </summary>
    /// <param name="sendRepository">
    /// Retrieves send records
    /// </param>
    /// <param name="cipherRepository">
    /// Confirms that the vault item an item-type send shares still exists
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sendRepository"/> or <paramref name="cipherRepository"/> is <see langword="null"/>.
    /// </exception>
    public SendAuthenticationQuery(ISendRepository sendRepository, ICipherRepository cipherRepository)
    {
        _sendRepository = sendRepository ?? throw new ArgumentNullException(nameof(sendRepository));
        _cipherRepository = cipherRepository ?? throw new ArgumentNullException(nameof(cipherRepository));
    }

    /// <inheritdoc cref="ISendAuthenticationQuery.GetAuthenticationMethod"/>
    public async Task<SendAuthenticationMethod> GetAuthenticationMethod(Guid sendId)
    {
        var send = await _sendRepository.GetByIdAsync(sendId);

        if (send != null && await ItemCipherIsMissingAsync(send))
        {
            return SEND_INACCESSIBLE;
        }

        SendAuthenticationMethod method = send switch
        {
            null => SEND_INACCESSIBLE,
            var s when s.Disabled => SEND_INACCESSIBLE,
            var s when s.AccessCount >= s.MaxAccessCount.GetValueOrDefault(int.MaxValue) => SEND_INACCESSIBLE,
            var s when s.ExpirationDate.GetValueOrDefault(DateTime.MaxValue) < DateTime.UtcNow => SEND_INACCESSIBLE,
            var s when s.DeletionDate <= DateTime.UtcNow => SEND_INACCESSIBLE,
            var s when s.AuthType == AuthType.Email && s.Emails is not null => EmailOtp(s.Emails),
            var s when s.AuthType == AuthType.Password && s.Password is not null => new ResourcePassword(s.Password),
            _ => NOT_AUTHENTICATED
        };

        return method;
    }

    /// <summary>
    /// An item-type Send is only accessible while the vault item it shares exists. The item id is read from the
    /// Send's data, so Sends created before <see cref="Send.CipherId"/> was populated are still checked.
    /// Sends whose item was deleted are denied here and removed by a background job.
    /// </summary>
    private async Task<bool> ItemCipherIsMissingAsync(Send send)
    {
        if (send.Type != SendType.Item)
        {
            return false;
        }

        var itemId = SendItemData.TryGetItemId(send.Data);
        return !itemId.HasValue || await _cipherRepository.GetByIdAsync(itemId.Value) == null;
    }

    private static EmailOtp EmailOtp(string? emails)
    {
        if (string.IsNullOrWhiteSpace(emails))
        {
            return new EmailOtp([]);
        }
        var list = emails.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new EmailOtp(list);
    }
}
