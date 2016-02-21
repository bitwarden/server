using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;
using Bit.Core.Domains;
using SendGrid;

namespace Bit.Core.Services
{
    public class MailService : IMailService
    {
        private const string WelcomeTemplateId = "d24aa21e-5ead-45d8-a14e-f96ba7ec63ff";
        private const string ChangeEmailAlreadyExistsTemplateId = "b28bc69e-9592-4320-b274-bfb955667add";
        private const string ChangeEmailTemplateId = "b8d17dd7-c883-4b47-8170-5b845d487929";
        private const string NoMasterPasswordHint = "d5d13bba-3f67-4899-9995-514c1bd6dae7";
        private const string MasterPasswordHint = "804a9897-1284-42e8-8aed-ab318c378b71";

        private const string AdministrativeCategoryName = "Administrative";
        private const string MarketingCategoryName = "Marketing";

        private readonly GlobalSettings _globalSettings;
        private readonly Web _web;

        public MailService(GlobalSettings globalSettings)
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
            var message = CreateDefaultMessage(NoMasterPasswordHint);

            message.Subject = "Your Master Password Hint";
            message.AddTo(email);
            message.SetCategories(new List<string> { AdministrativeCategoryName, "No Master Password Hint" });

            await _web.DeliverAsync(message);
        }

        public async Task SendMasterPasswordHintEmailAsync(string email, string hint)
        {
            var message = CreateDefaultMessage(MasterPasswordHint);

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
