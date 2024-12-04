using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;

public class VerifyOrganizationDomainCommand(
    IOrganizationDomainRepository organizationDomainRepository,
    IDnsResolverService dnsResolverService,
    IEventService eventService,
    IGlobalSettings globalSettings,
    IFeatureService featureService,
    ICurrentContext currentContext,
    ISavePolicyCommand savePolicyCommand,
    ILogger<VerifyOrganizationDomainCommand> logger)
    : IVerifyOrganizationDomainCommand
{


    public async Task<OrganizationDomain> UserVerifyOrganizationDomainAsync(OrganizationDomain organizationDomain)
    {
        if (currentContext.UserId is null)
        {
            throw new InvalidOperationException(
                $"{nameof(UserVerifyOrganizationDomainAsync)} can only be called by a user. " +
                $"Please call {nameof(SystemVerifyOrganizationDomainAsync)} for system users.");
        }

        var actingUser = new StandardUser(currentContext.UserId.Value, await currentContext.OrganizationOwner(organizationDomain.OrganizationId));

        var domainVerificationResult = await VerifyOrganizationDomainAsync(organizationDomain, actingUser);

        await eventService.LogOrganizationDomainEventAsync(domainVerificationResult,
            domainVerificationResult.VerifiedDate != null
                ? EventType.OrganizationDomain_Verified
                : EventType.OrganizationDomain_NotVerified);

        await organizationDomainRepository.ReplaceAsync(domainVerificationResult);

        return domainVerificationResult;
    }

    public async Task<OrganizationDomain> SystemVerifyOrganizationDomainAsync(OrganizationDomain organizationDomain)
    {
        var actingUser = new SystemUser(EventSystemUser.DomainVerification);

        organizationDomain.SetJobRunCount();

        var domainVerificationResult = await VerifyOrganizationDomainAsync(organizationDomain, actingUser);

        if (domainVerificationResult.VerifiedDate is not null)
        {
            logger.LogInformation(Constants.BypassFiltersEventId, "Successfully validated domain");

            await eventService.LogOrganizationDomainEventAsync(domainVerificationResult,
                EventType.OrganizationDomain_Verified,
                EventSystemUser.DomainVerification);
        }
        else
        {
            domainVerificationResult.SetNextRunDate(globalSettings.DomainVerification.VerificationInterval);

            await eventService.LogOrganizationDomainEventAsync(domainVerificationResult,
                EventType.OrganizationDomain_NotVerified,
                EventSystemUser.DomainVerification);

            logger.LogInformation(Constants.BypassFiltersEventId,
                "Verification for organization {OrgId} with domain {Domain} failed",
                domainVerificationResult.OrganizationId, domainVerificationResult.DomainName);
        }

        await organizationDomainRepository.ReplaceAsync(domainVerificationResult);

        return domainVerificationResult;
    }

    private async Task<OrganizationDomain> VerifyOrganizationDomainAsync(OrganizationDomain domain, IActingUser actingUser)
    {
        domain.SetLastCheckedDate();

        if (domain.VerifiedDate is not null)
        {
            await organizationDomainRepository.ReplaceAsync(domain);
            throw new ConflictException("Domain has already been verified.");
        }

        var claimedDomain =
            await organizationDomainRepository.GetClaimedDomainsByDomainNameAsync(domain.DomainName);

        if (claimedDomain.Count > 0)
        {
            await organizationDomainRepository.ReplaceAsync(domain);
            throw new ConflictException("The domain is not available to be claimed.");
        }

        try
        {
            if (await dnsResolverService.ResolveAsync(domain.DomainName, domain.Txt))
            {
                domain.SetVerifiedDate();

                await EnableSingleOrganizationPolicyAsync(domain.OrganizationId, actingUser);
            }
        }
        catch (Exception e)
        {
            logger.LogError("Error verifying Organization domain: {domain}. {errorMessage}",
                domain.DomainName, e.Message);
        }

        return domain;
    }

    private async Task EnableSingleOrganizationPolicyAsync(Guid organizationId, IActingUser actingUser)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning))
        {
            var policyUpdate = new PolicyUpdate
            {
                OrganizationId = organizationId,
                Type = PolicyType.SingleOrg,
                Enabled = true,
                PerformedBy = actingUser
            };

            await savePolicyCommand.SaveAsync(policyUpdate);
        }
    }
}
