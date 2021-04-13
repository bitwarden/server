using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Models.Mail;
using Bit.Core.Settings;
using System.IO;
using System.Net;
using Bit.Core.Utilities;
using System.Linq;
using System.Reflection;
using HandlebarsDotNet;

namespace Bit.Core.Services
{
    public class HandlebarsMailService : IMailService
    {
        private const string Namespace = "Bit.Core.MailTemplates.Handlebars";

        private readonly GlobalSettings _globalSettings;
        private readonly IMailDeliveryService _mailDeliveryService;
        private readonly Dictionary<string, Func<object, string>> _templateCache =
            new Dictionary<string, Func<object, string>>();

        private bool _registeredHelpersAndPartials = false;

        public HandlebarsMailService(
            GlobalSettings globalSettings,
            IMailDeliveryService mailDeliveryService)
        {
            _globalSettings = globalSettings;
            _mailDeliveryService = mailDeliveryService;
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
            await AddMessageContentAsync(message, "VerifyEmail", model);
            message.MetaData.Add("SendGridBypassListManagement", true);
            message.Category = "VerifyEmail";
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
            await AddMessageContentAsync(message, "VerifyDelete", model);
            message.MetaData.Add("SendGridBypassListManagement", true);
            message.Category = "VerifyDelete";
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
            await AddMessageContentAsync(message, "ChangeEmailAlreadyExists", model);
            message.Category = "ChangeEmailAlreadyExists";
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
            await AddMessageContentAsync(message, "ChangeEmail", model);
            message.MetaData.Add("SendGridBypassListManagement", true);
            message.Category = "ChangeEmail";
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
            await AddMessageContentAsync(message, "TwoFactorEmail", model);
            message.MetaData.Add("SendGridBypassListManagement", true);
            message.Category = "TwoFactorEmail";
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
            await AddMessageContentAsync(message, "MasterPasswordHint", model);
            message.Category = "MasterPasswordHint";
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
            await AddMessageContentAsync(message, "NoMasterPasswordHint", model);
            message.Category = "NoMasterPasswordHint";
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
            await AddMessageContentAsync(message, "OrganizationUserAccepted", model);
            message.Category = "OrganizationUserAccepted";
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
            await AddMessageContentAsync(message, "OrganizationUserConfirmed", model);
            message.Category = "OrganizationUserConfirmed";
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
            await SendOrganizationInviteEmailAsync(message, model);
        }

        public async Task BulkSendOrganizationInviteEmailAsync(string organizationName, IEnumerable<(OrganizationUser orgUser, string token)> invites)
        {
            MailMessage CreateMessage(string email)
            {
                var message = CreateDefaultMessage(null, email);
                message.Category = "OrganizationUserConfirmed";
                return message;
            }

            var messageModels = invites.Select(invite => (CreateMessage(invite.orgUser.Email),
                new OrganizationUserInvitedViewModel
                {
                    OrganizationName = CoreHelpers.SanitizeForEmail(organizationName),
                    Email = WebUtility.UrlEncode(invite.orgUser.Email),
                    OrganizationId = invite.orgUser.OrganizationId.ToString(),
                    OrganizationUserId = invite.orgUser.Id.ToString(),
                    Token = WebUtility.UrlEncode(invite.token),
                    OrganizationNameUrlEncoded = WebUtility.UrlEncode(organizationName),
                    WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                    SiteName = _globalSettings.SiteName,
                }
            ));

            await SendBulkAsync("OrganizationUserInvited", messageModels,
                (message, model) => SendOrganizationInviteEmailAsync(message, model),
                nameof(OrganizationUserInvitedViewModel.Url));
        }

        public async Task SendOrganizationUserRemovedForPolicyTwoStepEmailAsync(string organizationName, string email)
        {
            var message = CreateDefaultMessage($"You have been removed from {organizationName}", email);
            var model = new OrganizationUserRemovedForPolicyTwoStepViewModel
            {
                OrganizationName = CoreHelpers.SanitizeForEmail(organizationName),
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName
            };
            await AddMessageContentAsync(message, "OrganizationUserRemovedForPolicyTwoStep", model);
            message.Category = "OrganizationUserRemovedForPolicyTwoStep";
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendWelcomeEmailAsync(User user)
        {
            var message = CreateDefaultMessage("Welcome to Bitwarden!", user.Email);
            var model = new BaseMailModel
            {
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName
            };
            await AddMessageContentAsync(message, "Welcome", model);
            message.Category = "Welcome";
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
            await AddMessageContentAsync(message, "PasswordlessSignIn", model);
            message.Category = "PasswordlessSignIn";
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendInvoiceUpcomingAsync(string email, decimal amount, DateTime dueDate,
            List<string> items, bool mentionInvoices)
        {
            var message = CreateDefaultMessage("Your Subscription Will Renew Soon", email);
            var model = new InvoiceUpcomingViewModel
            {
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName,
                AmountDue = amount,
                DueDate = dueDate,
                Items = items,
                MentionInvoices = mentionInvoices
            };
            await AddMessageContentAsync(message, "InvoiceUpcoming", model);
            message.Category = "InvoiceUpcoming";
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendPaymentFailedAsync(string email, decimal amount, bool mentionInvoices)
        {
            var message = CreateDefaultMessage("Payment Failed", email);
            var model = new PaymentFailedViewModel
            {
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName,
                Amount = amount,
                MentionInvoices = mentionInvoices
            };
            await AddMessageContentAsync(message, "PaymentFailed", model);
            message.Category = "PaymentFailed";
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendAddedCreditAsync(string email, decimal amount)
        {
            var message = CreateDefaultMessage("Account Credit Payment Processed", email);
            var model = new AddedCreditViewModel
            {
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName,
                Amount = amount
            };
            await AddMessageContentAsync(message, "AddedCredit", model);
            message.Category = "AddedCredit";
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendLicenseExpiredAsync(IEnumerable<string> emails, string organizationName = null)
        {
            var message = CreateDefaultMessage("License Expired", emails);
            var model = new LicenseExpiredViewModel
            {
                OrganizationName = organizationName,
            };
            await AddMessageContentAsync(message, "LicenseExpired", model);
            message.Category = "LicenseExpired";
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendNewDeviceLoggedInEmail(string email, string deviceType, DateTime timestamp, string ip)
        {
            var message = CreateDefaultMessage($"New Device Logged In From {deviceType}", email);
            var model = new NewDeviceLoggedInModel
            {
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName,
                DeviceType = deviceType,
                TheDate = timestamp.ToLongDateString(),
                TheTime = timestamp.ToShortTimeString(),
                TimeZone = "UTC",
                IpAddress = ip
            };
            await AddMessageContentAsync(message, "NewDeviceLoggedIn", model);
            message.Category = "NewDeviceLoggedIn";
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendRecoverTwoFactorEmail(string email, DateTime timestamp, string ip)
        {
            var message = CreateDefaultMessage($"Recover 2FA From {ip}", email);
            var model = new RecoverTwoFactorModel
            {
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName,
                TheDate = timestamp.ToLongDateString(),
                TheTime = timestamp.ToShortTimeString(),
                TimeZone = "UTC",
                IpAddress = ip
            };
            await AddMessageContentAsync(message, "RecoverTwoFactor", model);
            message.Category = "RecoverTwoFactor";
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendOrganizationUserRemovedForPolicySingleOrgEmailAsync(string organizationName, string email)
        {
            var message = CreateDefaultMessage($"You have been removed from {organizationName}", email);
            var model = new OrganizationUserRemovedForPolicySingleOrgViewModel
            {
                OrganizationName = CoreHelpers.SanitizeForEmail(organizationName),
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName
            };
            await AddMessageContentAsync(message, "OrganizationUserRemovedForPolicySingleOrg", model);
            message.Category = "OrganizationUserRemovedForPolicySingleOrg";
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

        private async Task AddMessageContentAsync<T>(MailMessage message, string templateName, T model)
        {
            var (subject, html, text) = await GetMessageContentAsync(templateName, model);
            if (subject != null)
            {
                message.Subject = subject;
            }
            message.HtmlContent = html;
            message.TextContent = text;
        }

        private async Task<(string subjectPart, string htmlPart, string textPart)> GetMessageContentAsync<T>(string templateName, T model) =>
        (
            await RenderAsync($"{templateName}.subject", model),
            await RenderAsync($"{templateName}.html", model),
            await RenderAsync($"{templateName}.text", model)
        );

        private async Task<string> RenderAsync<T>(string templateName, T model)
        {
            await RegisterHelpersAndPartialsAsync();
            if (!_templateCache.TryGetValue(templateName, out var template))
            {
                var source = await ReadSourceAsync(templateName);
                if (source != null)
                {
                    template = Handlebars.Compile(source);
                    _templateCache.Add(templateName, template);
                }
            }
            return template != null ? template(model) : null;
        }

        private async Task<string> ReadSourceAsync(string templateName)
        {
            var assembly = typeof(HandlebarsMailService).GetTypeInfo().Assembly;
            var fullTemplateName = $"{Namespace}.{templateName}.hbs";
            if (!assembly.GetManifestResourceNames().Any(f => f == fullTemplateName))
            {
                return null;
            }
            using (var s = assembly.GetManifestResourceStream(fullTemplateName))
            using (var sr = new StreamReader(s))
            {
                return await sr.ReadToEndAsync();
            }
        }

        private async Task RegisterHelpersAndPartialsAsync()
        {
            if (_registeredHelpersAndPartials)
            {
                return;
            }
            _registeredHelpersAndPartials = true;

            var basicHtmlLayoutSource = await ReadSourceAsync("Layouts.Basic.html");
            Handlebars.RegisterTemplate("BasicHtmlLayout", basicHtmlLayoutSource);
            var basicTextLayoutSource = await ReadSourceAsync("Layouts.Basic.text");
            Handlebars.RegisterTemplate("BasicTextLayout", basicTextLayoutSource);
            var fullHtmlLayoutSource = await ReadSourceAsync("Layouts.Full.html");
            Handlebars.RegisterTemplate("FullHtmlLayout", fullHtmlLayoutSource);
            var fullTextLayoutSource = await ReadSourceAsync("Layouts.Full.text");
            Handlebars.RegisterTemplate("FullTextLayout", fullTextLayoutSource);

            Handlebars.RegisterHelper("date", (writer, context, parameters) =>
            {
                if (parameters.Length == 0 || !(parameters[0] is DateTime))
                {
                    writer.WriteSafeString(string.Empty);
                    return;
                }
                if (parameters.Length > 0 && parameters[1] is string)
                {
                    writer.WriteSafeString(((DateTime)parameters[0]).ToString(parameters[1].ToString()));
                }
                else
                {
                    writer.WriteSafeString(((DateTime)parameters[0]).ToString());
                }
            });

            Handlebars.RegisterHelper("usd", (writer, context, parameters) =>
            {
                if (parameters.Length == 0 || !(parameters[0] is decimal))
                {
                    writer.WriteSafeString(string.Empty);
                    return;
                }
                writer.WriteSafeString(((decimal)parameters[0]).ToString("C"));
            });

            Handlebars.RegisterHelper("link", (writer, context, parameters) =>
            {
                if (parameters.Length == 0)
                {
                    writer.WriteSafeString(string.Empty);
                    return;
                }

                var text = parameters[0].ToString();
                var href = text;
                var clickTrackingOff = false;
                if (parameters.Length == 2)
                {
                    if (parameters[1] is string)
                    {
                        var p1 = parameters[1].ToString();
                        if (p1 == "true" || p1 == "false")
                        {
                            clickTrackingOff = p1 == "true";
                        }
                        else
                        {
                            href = p1;
                        }
                    }
                    else if (parameters[1] is bool)
                    {
                        clickTrackingOff = (bool)parameters[1];
                    }
                }
                else if (parameters.Length > 2)
                {
                    if (parameters[1] is string)
                    {
                        href = parameters[1].ToString();
                    }
                    if (parameters[2] is string)
                    {
                        var p2 = parameters[2].ToString();
                        if (p2 == "true" || p2 == "false")
                        {
                            clickTrackingOff = p2 == "true";
                        }
                    }
                    else if (parameters[2] is bool)
                    {
                        clickTrackingOff = (bool)parameters[2];
                    }
                }

                var clickTrackingText = (clickTrackingOff ? "clicktracking=off" : string.Empty);
                writer.WriteSafeString($"<a href=\"{href}\" target=\"_blank\" {clickTrackingText}>{text}</a>");
            });
        }

        private async Task SendOrganizationInviteEmailAsync(MailMessage message, OrganizationUserInvitedViewModel model)
        {
            await AddMessageContentAsync(message, "OrganizationUserInvited", model);
            message.Category = "OrganizationUserInvited";
            await _mailDeliveryService.SendEmailAsync(message);
        }

        private async Task SendBulkAsync<T>(string templateName,
            IEnumerable<(MailMessage message, T model)> messageModels, Func<MailMessage, T, Task> fallbackSender,
            params string[] unescapedModelProperties)
        {
            if (_mailDeliveryService is AmazonSesMailDeliveryService sesService && messageModels.Skip(1).Any())
            {
                var bulkTemplate = GenerateModelForBulkSend(messageModels.First().model.GetType(), unescapedModelProperties);
                var (subjectPart, htmlPart, textPart) = await GetMessageContentAsync(templateName, bulkTemplate);

                await sesService.UpsertTemplate(templateName, subjectPart, textPart, htmlPart);

                await sesService.SendBulkTemplatedEmailAsync(templateName, bulkTemplate, messageModels);
            }
            else
            {
                foreach (var (message, model) in messageModels)
                {
                    await fallbackSender(message, model);
                }
            }
        }

        /// <summary>
        /// Creates a self-referential model which, if processed through Handlebars, creates a valid Handlebars template.
        /// The resulting template is a single-file template which can be uploaded to an SMTP service such as Amazon SES
        /// for bulk email requests.
        /// </summary>
        /// <param name="modelType">The type of the Handlebars model to make self-reference</param>
        /// <param name="unescapedProperties">The names of properties to encapsulate with triple-stash (`{{{}}}`) rather than double.
        /// These properties will not be escaped in when rendered. https://handlebarsjs.com/guide/expressions.html#html-escaping
        /// </param>
        /// <returns></returns>
        private Dictionary<string, string> GenerateModelForBulkSend(Type modelType, params string[] unescapedProperties)
        {
            var model = new Dictionary<string, string>();
            foreach (var pi in modelType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var escape = !unescapedProperties.Contains(pi.Name);
                model.Add(pi.Name, string.Concat(
                    escape ? "{{" : "{{{",
                    pi.Name,
                    escape ? "}}" : "}}}"
                ));
            }
            return model;
        }

        public async Task SendEmergencyAccessInviteEmailAsync(EmergencyAccess emergencyAccess, string name, string token)
        {
            var message = CreateDefaultMessage($"Emergency Access Contact Invite", emergencyAccess.Email);
            var model = new EmergencyAccessInvitedViewModel
            {
                Name = CoreHelpers.SanitizeForEmail(name),
                Email = WebUtility.UrlEncode(emergencyAccess.Email),
                Id = emergencyAccess.Id.ToString(),
                Token = WebUtility.UrlEncode(token),
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName
            };
            await AddMessageContentAsync(message, "EmergencyAccessInvited", model);
            message.Category = "EmergencyAccessInvited";
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendEmergencyAccessAcceptedEmailAsync(string granteeEmail, string email)
        {
            var message = CreateDefaultMessage($"Accepted Emergency Access", email);
            var model = new EmergencyAccessAcceptedViewModel
            {
                GranteeEmail = CoreHelpers.SanitizeForEmail(granteeEmail),
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName
            };
            await AddMessageContentAsync(message, "EmergencyAccessAccepted", model);
            message.Category = "EmergencyAccessAccepted";
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendEmergencyAccessConfirmedEmailAsync(string grantorName, string email)
        {
            var message = CreateDefaultMessage($"You Have Been Confirmed as Emergency Access Contact", email);
            var model = new EmergencyAccessConfirmedViewModel
            {
                Name = CoreHelpers.SanitizeForEmail(grantorName),
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                SiteName = _globalSettings.SiteName
            };
            await AddMessageContentAsync(message, "EmergencyAccessConfirmed", model);
            message.Category = "EmergencyAccessConfirmed";
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendEmergencyAccessRecoveryInitiated(EmergencyAccess emergencyAccess, string initiatingName, string email)
        {
            var message = CreateDefaultMessage("Emergency Access Initiated", email);

            var remainingTime = DateTime.UtcNow - emergencyAccess.RecoveryInitiatedDate.GetValueOrDefault();

            var model = new EmergencyAccessRecoveryViewModel
            {
                Name = CoreHelpers.SanitizeForEmail(initiatingName),
                Action = emergencyAccess.Type.ToString(),
                DaysLeft = emergencyAccess.WaitTimeDays - Convert.ToInt32((remainingTime).TotalDays),
            };
            await AddMessageContentAsync(message, "EmergencyAccessRecovery", model);
            message.Category = "EmergencyAccessRecovery";
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendEmergencyAccessRecoveryApproved(EmergencyAccess emergencyAccess, string approvingName, string email)
        {
            var message = CreateDefaultMessage("Emergency Access Approved", email);
            var model = new EmergencyAccessApprovedViewModel
            {
                Name = CoreHelpers.SanitizeForEmail(approvingName),
            };
            await AddMessageContentAsync(message, "EmergencyAccessApproved", model);
            message.Category = "EmergencyAccessApproved";
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendEmergencyAccessRecoveryRejected(EmergencyAccess emergencyAccess, string rejectingName, string email)
        {
            var message = CreateDefaultMessage("Emergency Access Rejected", email);
            var model = new EmergencyAccessRejectedViewModel
            {
                Name = CoreHelpers.SanitizeForEmail(rejectingName),
            };
            await AddMessageContentAsync(message, "EmergencyAccessRejected", model);
            message.Category = "EmergencyAccessRejected";
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendEmergencyAccessRecoveryReminder(EmergencyAccess emergencyAccess, string initiatingName, string email)
        {
            var message = CreateDefaultMessage("Pending Emergency Access Request", email);

            var remainingTime = DateTime.UtcNow - emergencyAccess.RecoveryInitiatedDate.GetValueOrDefault();

            var model = new EmergencyAccessRecoveryViewModel
            {
                Name = CoreHelpers.SanitizeForEmail(initiatingName),
                Action = emergencyAccess.Type.ToString(),
                DaysLeft = emergencyAccess.WaitTimeDays - Convert.ToInt32((remainingTime).TotalDays),
            };
            await AddMessageContentAsync(message, "EmergencyAccessRecoveryReminder", model);
            message.Category = "EmergencyAccessRecoveryReminder";
            await _mailDeliveryService.SendEmailAsync(message);
        }

        public async Task SendEmergencyAccessRecoveryTimedOut(EmergencyAccess emergencyAccess, string initiatingName, string email)
        {
            var message = CreateDefaultMessage("Emergency Access Granted", email);
            var model = new EmergencyAccessRecoveryTimedOutViewModel
            {
                Name = CoreHelpers.SanitizeForEmail(initiatingName),
                Action = emergencyAccess.Type.ToString(),
            };
            await AddMessageContentAsync(message, "EmergencyAccessRecoveryTimedOut", model);
            message.Category = "EmergencyAccessRecoveryTimedOut";
            await _mailDeliveryService.SendEmailAsync(message);
        }
    }
}
