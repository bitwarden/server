using System.Net;
using System.Threading.Tasks;
using Bit.CommCore.ProviderFeatures.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Entities.Provider;
using Bit.Core.Models.Mail;
using Bit.Core.Models.Mail.Provider;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.CommCore.ProviderFeatures
{
    public class ProviderMailer : IProviderMailer
    {

        private readonly IGlobalSettings _globalSettings;
        private readonly IMailService _mailService;

        public ProviderMailer(IGlobalSettings globalSettings, IMailService mailService)
        {
            _globalSettings = globalSettings;
            _mailService = mailService;
        }

        public async Task SendProviderSetupInviteEmailAsync(Provider provider, string token, string email)
        {
            var message = new MailMessage
            {
                Subject = "Create a Provider",
                ToEmails = new[] { email },
                Category = "ProviderSetupInvite",
            };
            var model = new ProviderSetupInviteViewModel
            {
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName,
                ProviderId = provider.Id.ToString(),
                Email = WebUtility.UrlEncode(email),
                Token = WebUtility.UrlEncode(token),
            };
            await _mailService.EnqueueMailAsync(message, "Provider.ProviderSetupInvite", model);
        }

        public async Task SendProviderInviteEmailAsync(Provider provider, ProviderUser providerUser, string token)
        {
            var message = new MailMessage
            {
                Subject = $"Join {provider.Name}",
                ToEmails = new[] { providerUser.Email },
                Category = "ProviderSetupInvite",
            };
            var model = new ProviderUserInvitedViewModel
            {
                ProviderName = CoreHelpers.SanitizeForEmail(provider.Name),
                Email = WebUtility.UrlEncode(providerUser.Email),
                ProviderId = providerUser.ProviderId.ToString(),
                ProviderUserId = providerUser.Id.ToString(),
                ProviderNameUrlEncoded = WebUtility.UrlEncode(provider.Name),
                Token = WebUtility.UrlEncode(token),
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName,
            };
            await _mailService.EnqueueMailAsync(message, "Provider.ProviderUserInvited", model);
        }

        public async Task SendProviderConfirmedEmailAsync(Provider provider, User user)
        {
            var message = new MailMessage
            {
                Subject = $"You Have Been Confirmed To {provider.Name}",
                ToEmails = new[] { user.Email },
                Category = "ProviderUserConfirmed",
            };
            var model = new ProviderUserConfirmedViewModel
            {
                ProviderName = CoreHelpers.SanitizeForEmail(provider.Name),
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName
            };
            await _mailService.EnqueueMailAsync(message, "Provider.ProviderUserConfirmed", model);
        }

        public async Task SendProviderUserRemoved(Provider provider, string email)
        {
            var message = new MailMessage
            {
                Subject = $"You Have Been Removed from {provider.Name}",
                ToEmails = new[] { email },
                Category = "ProviderUserRemoved",
            };
            var model = new ProviderUserRemovedViewModel
            {
                ProviderName = CoreHelpers.SanitizeForEmail(provider.Name),
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName
            };
            await _mailService.EnqueueMailAsync(message, "Provider.ProviderUserRemoved", model);
        }
    }
}
