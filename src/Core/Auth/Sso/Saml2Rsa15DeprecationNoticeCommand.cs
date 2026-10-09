using System.Globalization;
using Bit.Core.Auth.Sso.Mail;
using Bit.Core.Enums;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Auth.Sso;

public class Saml2Rsa15DeprecationNoticeCommand(
    // TODO: replace distributed cache usage w/ FusionCache
    [FromKeyedServices("persistent")] IDistributedCache cache,
    IGlobalSettings globalSettings,
    TimeProvider timeProvider,
    IOrganizationUserRepository organizationUserRepository,
    IMailer mailer,
    ILogger<Saml2Rsa15DeprecationNoticeCommand> logger) : ISaml2Rsa15DeprecationNoticeCommand
{
    public async Task SendEmailIfDueAsync(Guid organizationId)
    {
        try
        {
            if (!await IsEmailDueAsync(organizationId))
            {
                return;
            }

            // The email send cooldown starts before the email send, so other server instances see it while the email send runs.
            await StartEmailSendCooldownAsync(organizationId);
            try
            {
                await SendEmailToOwnersAndAdminsAsync(organizationId);
            }
            catch
            {
                // A failed email send must not block the next email send for a full cooldown.
                await TryRemoveEmailSendCooldownAsync(organizationId);
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send the RSA 1.5 deprecation email.");
        }
    }

    private TimeSpan EmailSendCooldown => TimeSpan.FromDays(globalSettings.Sso.Rsa15DeprecationEmailCooldownInDays);

    /// <summary>
    /// Determines whether the organization can receive the email now.
    /// </summary>
    /// <returns><c>false</c> when an email send cooldown is active or the email is off. Otherwise <c>true</c>.</returns>
    private async Task<bool> IsEmailDueAsync(Guid organizationId)
    {
        if (EmailSendCooldown <= TimeSpan.Zero)
        {
            return false;
        }

        var stored = await cache.GetStringAsync(CacheKey(organizationId));
        if (!long.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lastSentTicks))
        {
            return true;
        }

        var nowTicks = timeProvider.GetUtcNow().UtcTicks;
        return nowTicks - lastSentTicks >= EmailSendCooldown.Ticks;
    }

    /// <summary>
    /// Records the current time as the start of a new email send cooldown for the organization.
    /// </summary>
    private async Task StartEmailSendCooldownAsync(Guid organizationId)
    {
        var nowTicks = timeProvider.GetUtcNow().UtcTicks;
        await cache.SetStringAsync(
            CacheKey(organizationId),
            nowTicks.ToString(CultureInfo.InvariantCulture),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = EmailSendCooldown });
    }

    /// <summary>
    /// Removes the email send cooldown for the organization, so the next call can send the email.
    /// Logs a removal failure and never throws, so the caller keeps the exception of the failed email send.
    /// </summary>
    private async Task TryRemoveEmailSendCooldownAsync(Guid organizationId)
    {
        try
        {
            await cache.RemoveAsync(CacheKey(organizationId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove the RSA 1.5 deprecation email send cooldown.");
        }
    }

    private static string CacheKey(Guid organizationId) => $"sso:saml2:rsa15-deprecation-email:{organizationId}";

    /// <summary>
    /// Sends one RSA 1.5 deprecation email to the confirmed owners and admins of the organization.
    /// Sends nothing when the organization has no owner or admin email address.
    /// </summary>
    private async Task SendEmailToOwnersAndAdminsAsync(Guid organizationId)
    {
        var admins = await organizationUserRepository.GetManyByMinimumRoleAsync(organizationId, OrganizationUserType.Admin);

        var emails = admins
            .Select(a => a.Email)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (emails.Count == 0)
        {
            return;
        }

        await mailer.SendEmail(new Saml2Rsa15DeprecationMail
        {
            ToEmails = emails,
            View = new Saml2Rsa15DeprecationMailView()
        });
    }
}
