// FIXME: Update this file to be null safe and then delete the line below

using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Billing.Pricing;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Tools.Services;

public class SendValidationService : ISendValidationService
{
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserService _userService;
    private readonly GlobalSettings _globalSettings;
    private readonly IPolicyRequirementQuery _policyRequirementQuery;
    private readonly IPricingClient _pricingClient;

    public SendValidationService(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IUserService userService,
        IPolicyRequirementQuery policyRequirementQuery,
        GlobalSettings globalSettings,
        IPricingClient pricingClient)
    {
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _userService = userService;
        _policyRequirementQuery = policyRequirementQuery;
        _globalSettings = globalSettings;
        _pricingClient = pricingClient;
    }

    public async Task ValidateUserCanSaveAsync(Guid? userId, Send send)
    {
        // The below is unrelated to any policy available in the Admin Console but avoids a situation whereby
        // Emails may be sent as a plain-text string < 4000 chars during Send creation and subsequently protected using
        // ASP.NET Data Protection with encrypted value violating 4000 char limit
        if (!string.IsNullOrWhiteSpace(send.Emails))
        {
            // The "P|" prefix is a server-internal sentinel for Data-Protection-wrapped values.
            // Clients must not submit a value starting with this prefix.
            if (send.Emails.StartsWith(Constants.DatabaseFieldProtectedPrefix))
            {
                throw new BadRequestException("The Emails field contains an invalid character sequence.");
            }

            // 2500 plaintext chars → ~3450 chars after Data Protection wrap (P| + base64 +
            // ~84-byte binary header/MAC/IV), which fits the NVARCHAR(4000) column with headroom.
            if (send.Emails.Length > 2500)
            {
                throw new BadRequestException(
                    "The total number of characters in the Emails field must not exceed 2,500 characters.");
            }
        }

        // The nullable userId is intended to support organization-owned Sends (never implemented).
        // If it's null, we can't enforce policies, because policies are only enforced against a specific user.
        if (!userId.HasValue)
        {
            return;
        }

        // Once data migration has run, query only SendControls
        var sendControlsTask = _policyRequirementQuery.GetAsync<SendControlsPolicyRequirement>(userId.Value);
        var disableSendTask = _policyRequirementQuery.GetAsync<DisableSendPolicyRequirement>(userId.Value);
        var sendOptionsTask = _policyRequirementQuery.GetAsync<SendOptionsPolicyRequirement>(userId.Value);

        await Task.WhenAll(sendControlsTask, disableSendTask, sendOptionsTask);

        var sendControlsRequirement = sendControlsTask.Result;
        var disableSendRequirement = disableSendTask.Result;
        var sendOptionsRequirement = sendOptionsTask.Result;

        if (disableSendRequirement.DisableSend)
        {
            throw new BadRequestException("Due to an Enterprise Policy, you are only able to delete an existing Send.");
        }

        if (sendOptionsRequirement.DisableHideEmail && send.HideEmail.GetValueOrDefault())
        {
            throw new BadRequestException(
                "Due to an Enterprise Policy, you are not allowed to hide your email address from recipients when creating or editing a Send.");
        }

        var passwordRequired = sendControlsRequirement.WhoCanAccess == SendWhoCanAccessType.PasswordProtected;
        var emailsRequired = sendControlsRequirement.WhoCanAccess == SendWhoCanAccessType.SpecificPeople;
        if ((passwordRequired && send.Password == null) || (emailsRequired && send.Emails == null))
        {
            var requiredAccessControl = passwordRequired ? "password" : emailsRequired ? "email verification" : "(cannot determine required auth)";
            throw new BadRequestException($"Due to an Enterprise Policy your Sends must be protected by {requiredAccessControl}");
        }

        if (emailsRequired && sendControlsRequirement.AllowedDomains != null && !SendAllEmailsHaveAllowedDomains(send.Emails, sendControlsRequirement.AllowedDomains))
        {
            throw new BadRequestException($"Due to an Enterprise Policy your Sends must be protected by email verification and access granted only to the following domain(s): {sendControlsRequirement.AllowedDomains}");
        }

        if (sendControlsRequirement.AllowedSendTypes != null && !sendControlsRequirement.AllowedSendTypes.Contains(send.Type))
        {
            throw new BadRequestException($"Due to an Enterprise policy your Sends must be of the following types: {string.Join(", ", sendControlsRequirement.AllowedSendTypes.Select(st => st == SendType.Text ? "Text" : st == SendType.File ? "File" : "Unknown"))}");
        }

        if (sendControlsRequirement.DeletionHours != null && (send.DeletionDate - send.CreationDate).TotalHours > sendControlsRequirement.DeletionHours.Value + 1.0 / 60.0)
        {
            throw new BadRequestException($"Due to an Enterprise policy your Sends must have a deletion date no more than {sendControlsRequirement.DeletionHours} hours from its creation date");
        }
    }

    public static bool SendAllEmailsHaveAllowedDomains(string? emailsString, string? domainsString)
    {
        var domains = (domainsString ?? "").Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        // If we have no domains then any email is fine
        if (domains.Length == 0)
        {
            return true;
        }
        var emails = (emailsString ?? "").Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return emails.All(email => domains.Any(domain =>
        {
            var emailDomain = EmailValidation.GetDomain(email);
            return emailDomain.Equals(domain, StringComparison.OrdinalIgnoreCase)
                || emailDomain.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
        }));
    }

    public async Task<long> StorageRemainingForSendAsync(Send send)
    {
        var storageBytesRemaining = 0L;
        if (send.UserId.HasValue)
        {
            var user = await _userRepository.GetByIdAsync(send.UserId.Value) ?? throw new NotFoundException("Send user not found");
            if (!await _userService.CanAccessPremium(user))
            {
                throw new BadRequestException("You must have premium status to use file Sends.");
            }

            if (!user.EmailVerified)
            {
                throw new BadRequestException("You must confirm your email to use file Sends.");
            }

            if (user.Premium)
            {
                storageBytesRemaining = user.StorageBytesRemaining();
            }
            else
            {
                // Users that get access to file storage/premium from their organization get storage
                // based on the current premium plan from the pricing service
                short provided;
                if (_globalSettings.SelfHosted)
                {
                    provided = Constants.SelfHostedMaxStorageGb;
                }
                else
                {
                    var premiumPlan = await _pricingClient.GetAvailablePremiumPlan();
                    provided = (short)premiumPlan.Storage.Provided;
                }

                storageBytesRemaining = user.StorageBytesRemaining(provided);
            }
        }
        else if (send.OrganizationId.HasValue)
        {
            var org = await _organizationRepository.GetByIdAsync(send.OrganizationId.Value) ?? throw new NotFoundException("Send organization not found");
            if (!org.MaxStorageGb.HasValue)
            {
                throw new BadRequestException("This organization cannot use file sends.");
            }

            storageBytesRemaining = org.StorageBytesRemaining();
        }

        return storageBytesRemaining;
    }
}
