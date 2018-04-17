using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Models.Mail;
using System.IO;
using System.Net;
using System.Reflection;
using Bit.Core.Utilities;

namespace Bit.Core.Services
{
    public class MarkdownMailService : IMailService
    {
        private const string Namespace = "Bit.Core.MailTemplates.Markdown";

        private readonly GlobalSettings _globalSettings;
        private readonly IMailDeliveryService _mailDeliveryService;

        public MarkdownMailService(
            GlobalSettings globalSettings,
            IMailDeliveryService mailDeliveryService)
        {
            _globalSettings = globalSettings;
            _mailDeliveryService = mailDeliveryService;
        }

        public async Task SendVerifyEmailEmailAsync(string email, Guid userId, string token)
        {
            var model = new Dictionary<string, string>
            {
                ["url"] = string.Format("{0}/verify-email?userId={1}&token={2}",
                    _globalSettings.BaseServiceUri.VaultWithHash, userId, WebUtility.UrlEncode(token))
            };

            var message = await CreateMessageAsync("Verify Your Email", email, "VerifyEmail", model);
            message.MetaData.Add("SendGridBypassListManagement", true);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendVerifyDeleteEmailAsync(string email, Guid userId, string token)
        {
            var model = new Dictionary<string, string>
            {
                ["url"] = string.Format("{0}/verify-recover-delete?userId={1}&token={2}&email={3}",
                    _globalSettings.BaseServiceUri.VaultWithHash,
                    userId,
                    WebUtility.UrlEncode(token),
                    WebUtility.UrlEncode(email)),
                ["email"] = WebUtility.HtmlEncode(email)
            };

            var message = await CreateMessageAsync("Delete Your Account", email, "VerifyDelete", model);
            message.MetaData.Add("SendGridBypassListManagement", true);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendChangeEmailAlreadyExistsEmailAsync(string fromEmail, string toEmail)
        {
            var model = new Dictionary<string, string>
            {
                ["fromEmail"] = WebUtility.HtmlEncode(fromEmail),
                ["toEmail"] = WebUtility.HtmlEncode(toEmail),
            };

            var message = await CreateMessageAsync("Your Email Change", toEmail, "ChangeEmailAlreadyExists", model);
            message.MetaData.Add("SendGridBypassListManagement", true);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendChangeEmailEmailAsync(string newEmailAddress, string token)
        {
            var model = new Dictionary<string, string>
            {
                ["token"] = token
            };

            var message = await CreateMessageAsync("Your Email Change", newEmailAddress, "ChangeEmail", model);
            message.MetaData.Add("SendGridBypassListManagement", true);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendTwoFactorEmailAsync(string email, string token)
        {
            var model = new Dictionary<string, string>
            {
                ["token"] = token
            };

            var message = await CreateMessageAsync("Your Two-step Login Verification Code", email, "TwoFactorEmail", model);
            message.MetaData.Add("SendGridBypassListManagement", true);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendMasterPasswordHintEmailAsync(string email, string hint)
        {
            var model = new Dictionary<string, string>
            {
                ["hint"] = WebUtility.HtmlEncode(hint),
                ["vaultUrl"] = _globalSettings.BaseServiceUri.VaultWithHash
            };

            var message = await CreateMessageAsync("Your Master Password Hint", email, "MasterPasswordHint", model);
            message.MetaData.Add("SendGridBypassListManagement", true);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendNoMasterPasswordHintEmailAsync(string email)
        {
            var message = await CreateMessageAsync("Your Master Password Hint", email, "NoMasterPasswordHint", null);
            message.MetaData.Add("SendGridBypassListManagement", true);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendOrganizationAcceptedEmailAsync(string organizationName, string userEmail,
            IEnumerable<string> adminEmails)
        {
            var model = new Dictionary<string, string>
            {
                ["userEmail"] = WebUtility.HtmlEncode(userEmail),
                ["organizationName"] = WebUtility.HtmlEncode(organizationName)
            };

            var message = await CreateMessageAsync($"User {userEmail} Has Accepted Invite", adminEmails,
                "OrganizationUserAccepted", model);
            message.MetaData.Add("SendGridBypassListManagement", true);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendOrganizationConfirmedEmailAsync(string organizationName, string email)
        {
            var model = new Dictionary<string, string>
            {
                ["organizationName"] = WebUtility.HtmlEncode(organizationName)
            };

            var message = await CreateMessageAsync($"You Have Been Confirmed To {organizationName}", email,
                "OrganizationUserConfirmed", model);
            message.MetaData.Add("SendGridBypassListManagement", true);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendOrganizationInviteEmailAsync(string organizationName, OrganizationUser orgUser, string token)
        {
            var model = new Dictionary<string, string>
            {
                ["organizationName"] = WebUtility.HtmlEncode(organizationName),
                ["url"] = string.Format("{0}/accept-organization?organizationId={1}&organizationUserId={2}" +
                    "&email={3}&organizationName={4}&token={5}",
                    _globalSettings.BaseServiceUri.VaultWithHash,
                    orgUser.OrganizationId,
                    orgUser.Id,
                    WebUtility.UrlEncode(orgUser.Email),
                    WebUtility.UrlEncode(organizationName),
                    WebUtility.UrlEncode(token))
            };

            var message = await CreateMessageAsync($"Join {organizationName}", orgUser.Email, "OrganizationUserInvited", model);
            message.MetaData.Add("SendGridBypassListManagement", true);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendWelcomeEmailAsync(User user)
        {
            var model = new Dictionary<string, string>
            {
                ["vaultUrl"] = _globalSettings.BaseServiceUri.VaultWithHash
            };

            var message = await CreateMessageAsync("Welcome", user.Email, "Welcome", model);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendPasswordlessSignInAsync(string returnUrl, string token, string email)
        {
            var url = CoreHelpers.ExtendQuery(new Uri($"{_globalSettings.BaseServiceUri.Admin}/login/confirm"),
                new Dictionary<string, string>
                {
                    ["returnUrl"] = returnUrl,
                    ["email"] = email,
                    ["token"] = token,
                });
            var model = new Dictionary<string, string>
            {
                ["url"] = url.ToString()
            };

            var message = await CreateMessageAsync("[Admin] Continue Logging In", email, "PasswordlessSignIn", model);
            message.MetaData.Add("SendGridBypassListManagement", true);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        private async Task<MailMessage> CreateMessageAsync(string subject, string toEmail, string fileName,
            Dictionary<string, string> model)
        {
            return await CreateMessageAsync(subject, new List<string> { toEmail }, fileName, model);
        }

        private async Task<MailMessage> CreateMessageAsync(string subject, IEnumerable<string> toEmails, string fileName,
            Dictionary<string, string> model)
        {
            var message = new MailMessage
            {
                ToEmails = toEmails,
                Subject = subject,
                MetaData = new Dictionary<string, object>()
            };

            var assembly = typeof(MarkdownMailService).GetTypeInfo().Assembly;
            using(var s = assembly.GetManifestResourceStream($"{Namespace}.{fileName}.md"))
            using(var sr = new StreamReader(s))
            {
                var markdown = await sr.ReadToEndAsync();

                if(model != null)
                {
                    foreach(var prop in model)
                    {
                        markdown = markdown.Replace($"{{{{{prop.Key}}}}}", prop.Value);
                    }
                }

                message.HtmlContent = CommonMark.CommonMarkConverter.Convert(markdown);
                message.TextContent = markdown;
            }

            return message;
        }
    }
}
