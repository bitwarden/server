using System.Collections.Concurrent;
using Bit.Core.Auth.Sso;

namespace Bit.Sso.Utilities.Saml2;

public class Saml2Rsa15DeprecationNotifier(
    ISaml2Rsa15DeprecationNoticeCommand command,
    ILogger<Saml2Rsa15DeprecationNotifier> logger) : ISaml2Rsa15DeprecationNotifier
{
    // Organizations with an email send running on this server instance.
    // Concurrent logins for one organization start at most one email send.
    private readonly ConcurrentDictionary<Guid, byte> _pendingEmailSends = new();

    public void TryQueue(Guid organizationId)
    {
        try
        {
            if (!_pendingEmailSends.TryAdd(organizationId, 0))
            {
                return;
            }

            _ = Task.Run(() => SendEmailAsync(organizationId));
        }
        catch (Exception ex)
        {
            _pendingEmailSends.TryRemove(organizationId, out _);
            logger.LogError(ex, "Failed to queue the RSA 1.5 deprecation email.");
        }
    }

    private async Task SendEmailAsync(Guid organizationId)
    {
        try
        {
            await command.SendEmailIfDueAsync(organizationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run the background RSA 1.5 deprecation email send.");
        }
        finally
        {
            _pendingEmailSends.TryRemove(organizationId, out _);
        }
    }
}
