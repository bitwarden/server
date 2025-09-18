// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Tools.Services;

public class SendValidationService : ISendValidationService
{

    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IPolicyService _policyService;
    private readonly IFeatureService _featureService;
    private readonly IUserService _userService;
    private readonly GlobalSettings _globalSettings;
    private readonly ICurrentContext _currentContext;
    private readonly IPolicyRequirementQuery _policyRequirementQuery;



    public SendValidationService(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IPolicyService policyService,
        IFeatureService featureService,
        IUserService userService,
        IPolicyRequirementQuery policyRequirementQuery,
        GlobalSettings globalSettings,

        ICurrentContext currentContext)
    {
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _policyService = policyService;
        _featureService = featureService;
        _userService = userService;
        _policyRequirementQuery = policyRequirementQuery;
        _globalSettings = globalSettings;
        _currentContext = currentContext;
    }

    public async Task ValidateUserCanSaveAsync(Guid? userId, Send send)
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.PolicyRequirements))
        {
            await ValidateUserCanSaveAsync_vNext(userId, send);
            return;
        }

        if (!userId.HasValue || (!_currentContext.Organizations?.Any() ?? true))
        {
            return;
        }

        var anyDisableSendPolicies = await _policyService.AnyPoliciesApplicableToUserAsync(userId.Value,
            PolicyType.DisableSend);
        if (anyDisableSendPolicies)
        {
            throw new BadRequestException("Due to an Enterprise Policy, you are only able to delete an existing Send.");
        }

        if (send.HideEmail.GetValueOrDefault())
        {
            var sendOptionsPolicies = await _policyService.GetPoliciesApplicableToUserAsync(userId.Value, PolicyType.SendOptions);
            if (sendOptionsPolicies.Any(p => CoreHelpers.LoadClassFromJsonData<SendOptionsPolicyData>(p.PolicyData)?.DisableHideEmail ?? false))
            {
                throw new BadRequestException("Due to an Enterprise Policy, you are not allowed to hide your email address from recipients when creating or editing a Send.");
            }
        }
    }

    public async Task ValidateUserCanSaveAsync_vNext(Guid? userId, Send send)
    {
        if (!userId.HasValue)
        {
            return;
        }

        var disableSendRequirement = await _policyRequirementQuery.GetAsync<DisableSendPolicyRequirement>(userId.Value);
        if (disableSendRequirement.DisableSend)
        {
            throw new BadRequestException("Due to an Enterprise Policy, you are only able to delete an existing Send.");
        }

        var sendOptionsRequirement = await _policyRequirementQuery.GetAsync<SendOptionsPolicyRequirement>(userId.Value);
        if (sendOptionsRequirement.DisableHideEmail && send.HideEmail.GetValueOrDefault())
        {
            throw new BadRequestException("Due to an Enterprise Policy, you are not allowed to hide your email address from recipients when creating or editing a Send.");
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
                // Users that get access to file storage/premium from their organization get the default
                // 1 GB max storage.
                short limit = _globalSettings.SelfHosted ? Constants.SelfHostedMaxStorageGb : (short)1;
                storageBytesRemaining = user.StorageBytesRemaining(limit);
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
