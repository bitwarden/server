using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using RazorLight;
using Bit.Core.Models.Mail;
using RazorLight.Templating;
using System.IO;
using System.Net;

namespace Bit.Core.Services
{
    public class RazorViewMailService : IMailService
    {
        private readonly GlobalSettings _globalSettings;
        private readonly IRazorLightEngine _engine;
        private readonly IMailDeliveryService _mailDeliveryService;

        public RazorViewMailService(
            GlobalSettings globalSettings,
            IMailDeliveryService mailDeliveryService)
        {
            _globalSettings = globalSettings;
            _mailDeliveryService = mailDeliveryService;

            var manager = new CustomEmbeddedResourceTemplateManager("Bit.Core.MailTemplates");
            var core = new EngineCore(manager, EngineConfiguration.Default);
            var pageFactory = new DefaultPageFactory(core.KeyCompile);
            var lookup = new DefaultPageLookup(pageFactory);
            _engine = new RazorLightEngine(core, lookup);
        }

        public async Task SendVerifyEmailEmailAsync(string email, Guid userId, string token)
        {
            var message = CreateDefaultMessage("Verify Your Email", email);
            var model = new VerifyEmailModel
            {
                Token = WebUtility.UrlEncode(token),
                UserId = userId,
                WebVaultUrl = _globalSettings.BaseServiceUri.Vault,
                SiteName = _globalSettings.SiteName
            };
            message.HtmlContent = _engine.Parse("VerifyEmail", model);
            message.TextContent = _engine.Parse("VerifyEmail.text", model);
            message.MetaData.Add("SendGridBypassListManagement", true);

            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendChangeEmailAlreadyExistsEmailAsync(string fromEmail, string toEmail)
        {
            var message = CreateDefaultMessage("Your Email Change", toEmail);
            var model = new ChangeEmailExistsViewModel
            {
                FromEmail = fromEmail,
                ToEmail = toEmail,
                WebVaultUrl = _globalSettings.BaseServiceUri.Vault,
                SiteName = _globalSettings.SiteName
            };
            message.HtmlContent = _engine.Parse("ChangeEmailAlreadyExists", model);
            message.TextContent = _engine.Parse("ChangeEmailAlreadyExists.text", model);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendChangeEmailEmailAsync(string newEmailAddress, string token)
        {
            var message = CreateDefaultMessage("Your Email Change", newEmailAddress);
            var model = new EmailTokenViewModel
            {
                Token = token,
                WebVaultUrl = _globalSettings.BaseServiceUri.Vault,
                SiteName = _globalSettings.SiteName
            };
            message.HtmlContent = _engine.Parse("ChangeEmail", model);
            message.TextContent = _engine.Parse("ChangeEmail.text", model);
            message.MetaData.Add("SendGridBypassListManagement", true);

            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendTwoFactorEmailAsync(string email, string token)
        {
            var message = CreateDefaultMessage("Your Two-step Login Verification Code", email);
            var model = new EmailTokenViewModel
            {
                Token = token,
                WebVaultUrl = _globalSettings.BaseServiceUri.Vault,
                SiteName = _globalSettings.SiteName
            };
            message.HtmlContent = _engine.Parse("TwoFactorEmail", model);
            message.TextContent = _engine.Parse("TwoFactorEmail.text", model);
            message.MetaData.Add("SendGridBypassListManagement", true);

            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendMasterPasswordHintEmailAsync(string email, string hint)
        {
            var message = CreateDefaultMessage("Your Master Password Hint", email);
            var model = new MasterPasswordHintViewModel
            {
                Hint = hint,
                WebVaultUrl = _globalSettings.BaseServiceUri.Vault,
                SiteName = _globalSettings.SiteName
            };
            message.HtmlContent = _engine.Parse("MasterPasswordHint", model);
            message.TextContent = _engine.Parse("MasterPasswordHint.text", model);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendNoMasterPasswordHintEmailAsync(string email)
        {
            var message = CreateDefaultMessage("Your Master Password Hint", email);
            var model = new BaseMailModel
            {
                WebVaultUrl = _globalSettings.BaseServiceUri.Vault,
                SiteName = _globalSettings.SiteName
            };
            message.HtmlContent = _engine.Parse("NoMasterPasswordHint", model);
            message.TextContent = _engine.Parse("NoMasterPasswordHint.text", model);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendOrganizationAcceptedEmailAsync(string organizationName, string userEmail,
            IEnumerable<string> adminEmails)
        {
            var message = CreateDefaultMessage($"User {userEmail} Has Accepted Invite", adminEmails);
            var model = new OrganizationUserAcceptedViewModel
            {
                OrganizationName = organizationName,
                UserEmail = userEmail,
                WebVaultUrl = _globalSettings.BaseServiceUri.Vault,
                SiteName = _globalSettings.SiteName
            };
            message.HtmlContent = _engine.Parse("OrganizationUserInvited", model);
            message.TextContent = _engine.Parse("OrganizationUserInvited.text", model);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendOrganizationConfirmedEmailAsync(string organizationName, string email)
        {
            var message = CreateDefaultMessage($"You Have Been Confirmed To {organizationName}", email);
            var model = new OrganizationUserConfirmedViewModel
            {
                OrganizationName = organizationName,
                WebVaultUrl = _globalSettings.BaseServiceUri.Vault,
                SiteName = _globalSettings.SiteName
            };
            message.HtmlContent = _engine.Parse("OrganizationUserConfirmed", model);
            message.TextContent = _engine.Parse("OrganizationUserConfirmed.text", model);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendOrganizationInviteEmailAsync(string organizationName, OrganizationUser orgUser, string token)
        {
            var message = CreateDefaultMessage($"Join {organizationName}", orgUser.Email);
            var model = new OrganizationUserInvitedViewModel
            {
                OrganizationName = organizationName,
                Email = WebUtility.UrlEncode(orgUser.Email),
                OrganizationId = orgUser.OrganizationId.ToString(),
                OrganizationUserId = orgUser.Id.ToString(),
                Token = token,
                OrganizationNameUrlEncoded = WebUtility.UrlEncode(organizationName),
                WebVaultUrl = _globalSettings.BaseServiceUri.Vault,
                SiteName = _globalSettings.SiteName
            };
            message.HtmlContent = _engine.Parse("OrganizationUserInvited", model);
            message.TextContent = _engine.Parse("OrganizationUserInvited.text", model);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendWelcomeEmailAsync(User user)
        {
            var message = CreateDefaultMessage("Welcome", user.Email);
            var model = new BaseMailModel
            {
                WebVaultUrl = _globalSettings.BaseServiceUri.Vault,
                SiteName = _globalSettings.SiteName
            };
            message.HtmlContent = _engine.Parse("Welcome", model);
            message.TextContent = _engine.Parse("Welcome.text", model);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        private MailMessage CreateDefaultMessage(string subject, string toEmail)
        {
            return CreateDefaultMessage(subject, new List<string> { toEmail });
        }

        private MailMessage CreateDefaultMessage(string subject, IEnumerable<string> toEmails)
        {
            return new MailMessage
            {
                ToEmails = toEmails,
                Subject = subject,
                MetaData = new Dictionary<string, object>()
            };
        }

        public class CustomEmbeddedResourceTemplateManager : ITemplateManager
        {
            public CustomEmbeddedResourceTemplateManager(string rootNamespace)
            {
                Namespace = rootNamespace ?? throw new ArgumentNullException(nameof(rootNamespace));
            }

            public string Namespace { get; }

            public ITemplateSource Resolve(string key)
            {
                var assembly = GetType().Assembly;
                using(var stream = assembly.GetManifestResourceStream(Namespace + "." + key + ".cshtml"))
                {
                    if(stream == null)
                    {
                        throw new RazorLightException(string.Format("Couldn't load resource '{0}.{1}.cshtml'.", Namespace, key));
                    }

                    using(var reader = new StreamReader(stream))
                    {
                        return new LoadedTemplateSource(reader.ReadToEnd());
                    }
                }
            }
        }
    }
}
