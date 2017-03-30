using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Net;

namespace Bit.Core.Services
{
    public class SendGridMailService : IMailService
    {
        private const string WelcomeTemplateId = "045f8ad5-5547-4fa2-8d3d-6d46e401164d";
        private const string ChangeEmailAlreadyExistsTemplateId = "b69d2038-6ad9-4cf6-8f7f-7880921cba43";
        private const string ChangeEmailTemplateId = "ec2c1471-8292-4f17-b6b6-8223d514f86e";
        private const string NoMasterPasswordHintTemplateId = "136eb299-e102-495a-88bd-f96736eea159";
        private const string MasterPasswordHintTemplateId = "be77cfde-95dd-4cb9-b5e0-8286b53885f1";
        private const string OrganizationInviteTemplateId = "1eff5512-e36c-49a8-b9e2-2b215d6bbced";

        private const string AdministrativeCategoryName = "Administrative";
        private const string MarketingCategoryName = "Marketing";

        private readonly GlobalSettings _globalSettings;
        private readonly SendGridClient _client;

        public SendGridMailService(GlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;
            _client = new SendGridClient(_globalSettings.Mail.ApiKey);
        }

        public async Task SendWelcomeEmailAsync(User user)
        {
            var message = CreateDefaultMessage(WelcomeTemplateId);

            message.Subject = "Welcome";
            message.AddTo(new EmailAddress(user.Email));
            message.AddCategories(new List<string> { AdministrativeCategoryName, "Welcome" });

            await _client.SendEmailAsync(message);
        }

        public async Task SendChangeEmailAlreadyExistsEmailAsync(string fromEmail, string toEmail)
        {
            var message = CreateDefaultMessage(ChangeEmailAlreadyExistsTemplateId);

            message.Subject = "Your Email Change";
            message.AddTo(new EmailAddress(toEmail));
            message.AddSubstitution("{{fromEmail}}", fromEmail);
            message.AddSubstitution("{{toEmail}}", toEmail);
            message.AddCategories(new List<string> { AdministrativeCategoryName, "Change Email Alrady Exists" });

            await _client.SendEmailAsync(message);
        }

        public async Task SendChangeEmailEmailAsync(string newEmailAddress, string token)
        {
            var message = CreateDefaultMessage(ChangeEmailTemplateId);

            message.Subject = "Change Your Email";
            message.AddTo(new EmailAddress(newEmailAddress));
            message.AddSubstitution("{{token}}", Uri.EscapeDataString(token));
            message.AddCategories(new List<string> { AdministrativeCategoryName, "Change Email" });
            message.SetBypassListManagement(true);

            await _client.SendEmailAsync(message);
        }

        public async Task SendNoMasterPasswordHintEmailAsync(string email)
        {
            var message = CreateDefaultMessage(NoMasterPasswordHintTemplateId);

            message.Subject = "Your Master Password Hint";
            message.AddTo(new EmailAddress(email));
            message.AddCategories(new List<string> { AdministrativeCategoryName, "No Master Password Hint" });

            await _client.SendEmailAsync(message);
        }

        public async Task SendMasterPasswordHintEmailAsync(string email, string hint)
        {
            var message = CreateDefaultMessage(MasterPasswordHintTemplateId);

            message.Subject = "Your Master Password Hint";
            message.AddTo(new EmailAddress(email));
            message.AddSubstitution("{{hint}}", hint);
            message.AddCategories(new List<string> { AdministrativeCategoryName, "Master Password Hint" });

            await _client.SendEmailAsync(message);
        }

        public async Task SendOrganizationInviteEmailAsync(string organizationName, OrganizationUser orgUser, string token)
        {
            var message = CreateDefaultMessage(OrganizationInviteTemplateId);

            message.Subject = $"Join {organizationName}";
            message.AddTo(new EmailAddress(orgUser.Email));
            message.AddSubstitution("{{organizationName}}", organizationName);
            message.AddSubstitution("{{organizationId}}", orgUser.OrganizationId.ToString());
            message.AddSubstitution("{{organizationUserId}}", orgUser.Id.ToString());
            message.AddSubstitution("{{token}}", token);
            message.AddSubstitution("{{email}}", WebUtility.UrlEncode(orgUser.Email));
            message.AddSubstitution("{{organizationNameUrlEncoded}}", WebUtility.UrlEncode(organizationName));
            message.AddCategories(new List<string> { AdministrativeCategoryName, "Organization Invite" });

            await _client.SendEmailAsync(message);
        }

        private SendGridMessage CreateDefaultMessage(string templateId)
        {
            var message = new SendGridMessage
            {
                From = new EmailAddress(_globalSettings.Mail.ReplyToEmail, _globalSettings.SiteName),
                HtmlContent = " ",
                PlainTextContent = " "
            };

            if(!string.IsNullOrWhiteSpace(templateId))
            {
                message.TemplateId = templateId;
            }

            message.AddSubstitution("{{siteName}}", _globalSettings.SiteName);
            message.AddSubstitution("{{baseVaultUri}}", _globalSettings.BaseVaultUri);

            return message;
        }
    }
}
