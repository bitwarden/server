using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;
using Bit.Core.Domains;
using SendGrid;

namespace Bit.Core.Services
{
    public class SendGridMailService : IMailService
    {
        private const string WelcomeTemplateId = "045f8ad5-5547-4fa2-8d3d-6d46e401164d";
        private const string ChangeEmailAlreadyExistsTemplateId = "b69d2038-6ad9-4cf6-8f7f-7880921cba43";
        private const string ChangeEmailTemplateId = "ec2c1471-8292-4f17-b6b6-8223d514f86e";
        private const string NoMasterPasswordHintTemplateId = "136eb299-e102-495a-88bd-f96736eea159";
        private const string MasterPasswordHintTemplateId = "be77cfde-95dd-4cb9-b5e0-8286b53885f1";

        private const string AdministrativeCategoryName = "Administrative";
        private const string MarketingCategoryName = "Marketing";

        private readonly GlobalSettings _globalSettings;
        private readonly Web _web;

        public SendGridMailService(GlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;
            _web = new Web(_globalSettings.Mail.ApiKey);
        }

        public async Task SendWelcomeEmailAsync(User user)
        {
            var message = CreateDefaultMessage(WelcomeTemplateId);

            message.Subject = "Welcome";
            message.AddTo(user.Email);
            message.SetCategories(new List<string> { AdministrativeCategoryName, "Welcome" });

            await _web.DeliverAsync(message);
        }

        public async Task SendChangeEmailAlreadyExistsEmailAsync(string fromEmail, string toEmail)
        {
            var message = CreateDefaultMessage(ChangeEmailAlreadyExistsTemplateId);

            message.Subject = "Your Email Change";
            message.AddTo(toEmail);
            message.AddSubstitution("{{fromEmail}}", new List<string> { fromEmail });
            message.AddSubstitution("{{toEmail}}", new List<string> { toEmail });
            message.SetCategories(new List<string> { AdministrativeCategoryName, "Change Email Alrady Exists" });

            await _web.DeliverAsync(message);
        }

        public async Task SendChangeEmailEmailAsync(string newEmailAddress, string token)
        {
            var message = CreateDefaultMessage(ChangeEmailTemplateId);

            message.Subject = "Change Your Email";
            message.AddTo(newEmailAddress);
            message.AddSubstitution("{{token}}", new List<string> { Uri.EscapeDataString(token) });
            message.SetCategories(new List<string> { AdministrativeCategoryName, "Change Email" });
            message.DisableBypassListManagement();

            await _web.DeliverAsync(message);
        }

        public async Task SendNoMasterPasswordHintEmailAsync(string email)
        {
            var message = CreateDefaultMessage(NoMasterPasswordHintTemplateId);

            message.Subject = "Your Master Password Hint";
            message.AddTo(email);
            message.SetCategories(new List<string> { AdministrativeCategoryName, "No Master Password Hint" });

            await _web.DeliverAsync(message);
        }

        public async Task SendMasterPasswordHintEmailAsync(string email, string hint)
        {
            var message = CreateDefaultMessage(MasterPasswordHintTemplateId);

            message.Subject = "Your Master Password Hint";
            message.AddTo(email);
            message.AddSubstitution("{{hint}}", new List<string> { hint });
            message.SetCategories(new List<string> { AdministrativeCategoryName, "Master Password Hint" });

            await _web.DeliverAsync(message);
        }

        private SendGridMessage CreateDefaultMessage(string templateId)
        {
            var message = new SendGridMessage
            {
                From = new MailAddress(_globalSettings.Mail.ReplyToEmail, _globalSettings.SiteName),
                Html = " ",
                Text = " "
            };

            if(!string.IsNullOrWhiteSpace(templateId))
            {
                message.EnableTemplateEngine(templateId);
            }

            message.AddSubstitution("{{siteName}}", new List<string> { _globalSettings.SiteName });
            message.AddSubstitution("{{baseVaultUri}}", new List<string> { _globalSettings.BaseVaultUri });

            return message;
        }
    }
}
