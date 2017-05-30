using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using RazorLight;
using Bit.Core.Models.Mail;
using RazorLight.Templating;
using System.IO;

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

            var manager = new CustomEmbeddedResourceTemplateManager("Bit.Core.MailTemplates");
            var core = new EngineCore(manager, EngineConfiguration.Default);
            var pageFactory = new DefaultPageFactory(core.KeyCompile);
            var lookup = new DefaultPageLookup(pageFactory);
            _engine = new RazorLightEngine(core, lookup);
        }

        public Task SendChangeEmailAlreadyExistsEmailAsync(string fromEmail, string toEmail)
        {
            throw new NotImplementedException();
        }

        public Task SendChangeEmailEmailAsync(string newEmailAddress, string token)
        {
            throw new NotImplementedException();
        }

        public async Task SendMasterPasswordHintEmailAsync(string email, string hint)
        {
            var message = CreateDefaultMessage("Your Master Password Hint", email);
            var model = new MasterPasswordHintViewModel
            {
                Hint = hint
            };
            message.HtmlContent = _engine.Parse("MasterPasswordHint", model);
            message.TextContent = _engine.Parse("MasterPasswordHint.text", model);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendNoMasterPasswordHintEmailAsync(string email)
        {
            var message = CreateDefaultMessage("Your Master Password Hint", email);
            var model = new BaseMailModel();
            message.HtmlContent = _engine.Parse("NoMasterPasswordHint", model);
            message.TextContent = _engine.Parse("NoMasterPasswordHint.text", model);
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public Task SendOrganizationAcceptedEmailAsync(string organizationName, string userEmail, IEnumerable<string> adminEmails)
        {
            throw new NotImplementedException();
        }

        public Task SendOrganizationConfirmedEmailAsync(string organizationName, string email)
        {
            throw new NotImplementedException();
        }

        public Task SendOrganizationInviteEmailAsync(string organizationName, OrganizationUser orgUser, string token)
        {
            throw new NotImplementedException();
        }

        public Task SendWelcomeEmailAsync(User user)
        {
            throw new NotImplementedException();
        }

        private MailMessage CreateDefaultMessage(string subject, string toEmail)
        {
            return CreateDefaultMessage(subject, new List<string> { toEmail });
        }

        private MailMessage CreateDefaultMessage(string subject, IEnumerable<string> toEmails)
        {
            var message = new MailMessage
            {
                MetaData = new Dictionary<string, object>(),
                ToEmails = toEmails,
                Subject = subject
            };

            return message;
        }

        public class CustomEmbeddedResourceTemplateManager : ITemplateManager
        {
            public CustomEmbeddedResourceTemplateManager(string rootNamespace)
            {
                if(rootNamespace == null)
                {
                    throw new ArgumentNullException(nameof(rootNamespace));
                }

                Namespace = rootNamespace;
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
