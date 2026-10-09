using System.Collections.Concurrent;
using Bit.Core.Auth.Sso;

namespace Bit.Sso.Utilities.Saml2;

public class Saml2Rsa15DeprecationNotifier(
    ISaml2Rsa15DeprecationNoticeInterval interval,
    ISaml2Rsa15DeprecationNoticeCommand command,
    ILogger<Saml2Rsa15DeprecationNotifier> logger) : ISaml2Rsa15DeprecationNotifier
{
    // The in-flight claims guard. An entry means that the organization has a claim processing on this server instance,
    // attempting to reserve the send interval in persistent cache.
    // The notifier removes the entry when the claim returns or throws.
    private readonly ConcurrentDictionary<Guid, byte> _claimsInFlight = new();

    public void TryQueue(Guid organizationId)
    {
        try
        {
            if (!_claimsInFlight.TryAdd(organizationId, 0))
            {
                return;
            }

            _ = Task.Run(() => ClaimAndSendAsync(organizationId));
        }
        catch (Exception ex)
        {
            _claimsInFlight.TryRemove(organizationId, out _);
            logger.LogError(ex, "Failed to queue the RSA 1.5 deprecation email.");
        }
    }

    private async Task ClaimAndSendAsync(Guid organizationId)
    {
        try
        {
            bool claimed;
            try
            {
                claimed = await interval.TryClaimIntervalAsync(organizationId);
            }
            finally
            {
                _claimsInFlight.TryRemove(organizationId, out _);
            }

            if (claimed)
            {
                await command.SendAsync(organizationId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to claim or send the RSA 1.5 deprecation email.");
        }
    }
}
