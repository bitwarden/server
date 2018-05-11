using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using RazorLight;
using Bit.Core.Models.Mail;
using System.IO;
using System.Net;
using Bit.Core.Utilities;
using RazorLight.Razor;
using System.Linq;
using System.Reflection;

namespace Bit.Core.Services
{
    public class RazorMailService : IMailService
    {
        private readonly GlobalSettings _globalSettings;
        private readonly IRazorLightEngine _engine;
        private readonly IMailDeliveryService _mailDeliveryService;

        public RazorMailService(
            GlobalSettings globalSettings,
            IMailDeliveryService mailDeliveryService)
        {
            _globalSettings = globalSettings;
            _mailDeliveryService = mailDeliveryService;


            var factory = new EngineFactory();
            _engine = factory.Create(new CustomEmbeddedRazorProject());
        }

        public async Task SendVerifyEmailEmailAsync(string email, Guid userId, string token)
        {
            var message = CreateDefaultMessage("Verify Your Email", email);
            var model = new VerifyEmailModel
            {
                Token = WebUtility.UrlEncode(token),
                UserId = userId,
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName
            };
            message.HtmlContent = await _engine.CompileRenderAsync("VerifyEmail", model);
            message.TextContent = await _engine.CompileRenderAsync("VerifyEmail.text", model);
            message.MetaData.Add("SendGridBypassListManagement", true);

            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendVerifyDeleteEmailAsync(string email, Guid userId, string token)
        {
            var message = CreateDefaultMessage("Delete Your Account", email);
            var model = new VerifyDeleteModel
            {
                Token = WebUtility.UrlEncode(token),
                UserId = userId,
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName,
                Email = email,
                EmailEncoded = WebUtility.UrlEncode(email)
            };
            message.HtmlContent = await _engine.CompileRenderAsync("VerifyDelete", model);
            message.TextContent = await _engine.CompileRenderAsync("VerifyDelete.text", model);
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
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName
            };
            message.HtmlContent = await _engine.CompileRenderAsync("ChangeEmailAlreadyExists", model);
            message.TextContent = await _engine.CompileRenderAsync("ChangeEmailAlreadyExists.text", model);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendChangeEmailEmailAsync(string newEmailAddress, string token)
        {
            var message = CreateDefaultMessage("Your Email Change", newEmailAddress);
            var model = new EmailTokenViewModel
            {
                Token = token,
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName
            };
            message.HtmlContent = await _engine.CompileRenderAsync("ChangeEmail", model);
            message.TextContent = await _engine.CompileRenderAsync("ChangeEmail.text", model);
            message.MetaData.Add("SendGridBypassListManagement", true);

            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendTwoFactorEmailAsync(string email, string token)
        {
            var message = CreateDefaultMessage("Your Two-step Login Verification Code", email);
            var model = new EmailTokenViewModel
            {
                Token = token,
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName
            };
            message.HtmlContent = await _engine.CompileRenderAsync("TwoFactorEmail", model);
            message.TextContent = await _engine.CompileRenderAsync("TwoFactorEmail.text", model);
            message.MetaData.Add("SendGridBypassListManagement", true);

            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendMasterPasswordHintEmailAsync(string email, string hint)
        {
            var message = CreateDefaultMessage("Your Master Password Hint", email);
            var model = new MasterPasswordHintViewModel
            {
                Hint = CoreHelpers.SanitizeForEmail(hint),
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName
            };
            message.HtmlContent = await _engine.CompileRenderAsync("MasterPasswordHint", model);
            message.TextContent = await _engine.CompileRenderAsync("MasterPasswordHint.text", model);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendNoMasterPasswordHintEmailAsync(string email)
        {
            var message = CreateDefaultMessage("Your Master Password Hint", email);
            var model = new BaseMailModel
            {
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName
            };
            message.HtmlContent = await _engine.CompileRenderAsync("NoMasterPasswordHint", model);
            message.TextContent = await _engine.CompileRenderAsync("NoMasterPasswordHint.text", model);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendOrganizationAcceptedEmailAsync(string organizationName, string userEmail,
            IEnumerable<string> adminEmails)
        {
            var message = CreateDefaultMessage($"User {userEmail} Has Accepted Invite", adminEmails);
            var model = new OrganizationUserAcceptedViewModel
            {
                OrganizationName = CoreHelpers.SanitizeForEmail(organizationName),
                UserEmail = userEmail,
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName
            };
            message.HtmlContent = await _engine.CompileRenderAsync("OrganizationUserAccepted", model);
            message.TextContent = await _engine.CompileRenderAsync("OrganizationUserAccepted.text", model);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendOrganizationConfirmedEmailAsync(string organizationName, string email)
        {
            var message = CreateDefaultMessage($"You Have Been Confirmed To {organizationName}", email);
            var model = new OrganizationUserConfirmedViewModel
            {
                OrganizationName = CoreHelpers.SanitizeForEmail(organizationName),
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName
            };
            message.HtmlContent = await _engine.CompileRenderAsync("OrganizationUserConfirmed", model);
            message.TextContent = await _engine.CompileRenderAsync("OrganizationUserConfirmed.text", model);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendOrganizationInviteEmailAsync(string organizationName, OrganizationUser orgUser, string token)
        {
            var message = CreateDefaultMessage($"Join {organizationName}", orgUser.Email);
            var model = new OrganizationUserInvitedViewModel
            {
                OrganizationName = CoreHelpers.SanitizeForEmail(organizationName),
                Email = WebUtility.UrlEncode(orgUser.Email),
                OrganizationId = orgUser.OrganizationId.ToString(),
                OrganizationUserId = orgUser.Id.ToString(),
                Token = WebUtility.UrlEncode(token),
                OrganizationNameUrlEncoded = WebUtility.UrlEncode(organizationName),
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName
            };
            message.HtmlContent = await _engine.CompileRenderAsync("OrganizationUserInvited", model);
            message.TextContent = await _engine.CompileRenderAsync("OrganizationUserInvited.text", model);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendWelcomeEmailAsync(User user)
        {
            var message = CreateDefaultMessage("Welcome", user.Email);
            var model = new BaseMailModel
            {
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName
            };
            message.HtmlContent = await _engine.CompileRenderAsync("Welcome", model);
            message.TextContent = await _engine.CompileRenderAsync("Welcome.text", model);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendPasswordlessSignInAsync(string returnUrl, string token, string email)
        {
            var message = CreateDefaultMessage("[Admin] Continue Logging In", email);

            var url = CoreHelpers.ExtendQuery(new Uri($"{_globalSettings.BaseServiceUri.Admin}/login/confirm"),
                new Dictionary<string, string>
                {
                    ["returnUrl"] = returnUrl,
                    ["email"] = email,
                    ["token"] = token,
                });
            var model = new PasswordlessSignInModel
            {
                Url = url.ToString()
            };
            message.HtmlContent = await _engine.CompileRenderAsync("PasswordlessSignIn", model);
            message.TextContent = await _engine.CompileRenderAsync("PasswordlessSignIn.text", model);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendInvoiceUpcomingAsync(string email, decimal amount, DateTime dueDate,
            List<string> items, bool mentionInvoices)
        {
            var message = CreateDefaultMessage("Your Subscription Will Renew Soon", email);
            message.BccEmails = new List<string> { "kyle@bitwarden.com" };

            var model = new InvoiceUpcomingViewModel
            {
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName,
                AmountDue = amount,
                DueDate = dueDate,
                Items = items,
                MentionInvoices = mentionInvoices
            };
            message.HtmlContent = await _engine.CompileRenderAsync("InvoiceUpcoming", model);
            message.TextContent = await _engine.CompileRenderAsync("InvoiceUpcoming.text", model);
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

        public class CustomEmbeddedRazorProject : RazorLightProject
        {
            public override Task<RazorLightProjectItem> GetItemAsync(string templateKey)
            {
                if(string.IsNullOrWhiteSpace(templateKey))
                {
                    throw new ArgumentNullException(nameof(templateKey));
                }

                var item = new CustomEmbeddedRazorProjectItem(templateKey);
                return Task.FromResult(item as RazorLightProjectItem);
            }

            public override Task<IEnumerable<RazorLightProjectItem>> GetImportsAsync(string templateKey)
            {
                return Task.FromResult(Enumerable.Empty<RazorLightProjectItem>());
            }
        }

        public class CustomEmbeddedRazorProjectItem : RazorLightProjectItem
        {
            private readonly string _fullTemplateKey;
            private readonly Assembly _assembly;

            public CustomEmbeddedRazorProjectItem(string key)
            {
                if(string.IsNullOrWhiteSpace(key))
                {
                    throw new ArgumentNullException(nameof(key));
                }

                Key = key;
                _assembly = GetType().Assembly;
                _fullTemplateKey = $"Bit.Core.MailTemplates.Razor.{key}.cshtml";
            }

            public override string Key { get; set; }
            public override bool Exists => _assembly.GetManifestResourceNames().Any(f => f == _fullTemplateKey);
            public override Stream Read() => _assembly.GetManifestResourceStream(_fullTemplateKey);
        }
    }
}
