// FIXME: Update this file to be null safe and then delete the line below

#nullable disable

using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Billing.Pricing;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;

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
        // var sendControlsTask = _policyRequirementQuery.GetAsync<SendControlsPolicyRequirement>(userId.Value);
        var disableSendTask = _policyRequirementQuery.GetAsync<DisableSendPolicyRequirement>(userId.Value);
        var sendOptionsTask = _policyRequirementQuery.GetAsync<SendOptionsPolicyRequirement>(userId.Value);

        await Task.WhenAll(disableSendTask, sendOptionsTask);

        // var sendControlsRequirement = sendControlsTask.Result;
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
    }

    public async Task<long> StorageRemainingForSendAsync(Send send)
    {
        var storageBytesRemaining = 0L;
        if (send.UserId.HasValue)
        {
            var user = await _userRepository.GetByIdAsync(send.UserId.Value);
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
            var org = await _organizationRepository.GetByIdAsync(send.OrganizationId.Value);
            if (!org.MaxStorageGb.HasValue)
            {
                throw new BadRequestException("This organization cannot use file sends.");
            }

            storageBytesRemaining = org.StorageBytesRemaining();
        }

        return storageBytesRemaining;
    }
}
