using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;

namespace Bit.Core.Services
{
    public class PolicyService : IPolicyService
    {
        private readonly IEventService _eventService;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IPolicyRepository _policyRepository;
        private readonly IMailService _mailService;

        public PolicyService(
            IEventService eventService,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IPolicyRepository policyRepository,
            IMailService mailService)
        {
            _eventService = eventService;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _policyRepository = policyRepository;
            _mailService = mailService;
        }

        public async Task SaveAsync(Policy policy, IUserService userService, IOrganizationService organizationService,
            Guid? savingUserId)
        {
            var org = await _organizationRepository.GetByIdAsync(policy.OrganizationId);
            if (org == null)
            {
                throw new BadRequestException("Organization not found");
            }

            if (!org.UsePolicies)
            {
                throw new BadRequestException("This organization cannot use policies.");
            }
            
            // Handle dependent policy checks
            switch(policy.Type)
            {
                case PolicyType.SingleOrg:
                    if (!policy.Enabled)
                    {
                        var requireSso =
                            await _policyRepository.GetByOrganizationIdTypeAsync(org.Id, PolicyType.RequireSso);
                        if (requireSso?.Enabled == true)
                        {
                            throw new BadRequestException("Single Sign-On Authentication policy is enabled.");
                        }
                    }
                    break;
                
               case PolicyType.RequireSso:
                   if (policy.Enabled)
                   {
                       var singleOrg = await _policyRepository.GetByOrganizationIdTypeAsync(org.Id, PolicyType.SingleOrg);
                       if (singleOrg?.Enabled != true)
                       {
                           throw new BadRequestException("Single Organization policy not enabled.");
                       }
                   }
                   break;
            }

            var now = DateTime.UtcNow;
            if (policy.Id == default(Guid))
            {
                policy.CreationDate = now;
            }
            else if (policy.Enabled)
            {
                var currentPolicy = await _policyRepository.GetByIdAsync(policy.Id);
                if (!currentPolicy?.Enabled ?? true)
                {
                    Organization organization = null;
                    var orgUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(
                        policy.OrganizationId);
                    var removableOrgUsers = orgUsers.Where(ou =>
                        ou.Status != Enums.OrganizationUserStatusType.Invited &&
                        ou.Type != Enums.OrganizationUserType.Owner && ou.Type != Enums.OrganizationUserType.Admin && 
                        ou.UserId != savingUserId);
                    switch (currentPolicy.Type)
                    {
                        case Enums.PolicyType.TwoFactorAuthentication:
                            foreach (var orgUser in removableOrgUsers)
                            {
                                if (!await userService.TwoFactorIsEnabledAsync(orgUser))
                                {
                                    organization = organization ?? await _organizationRepository.GetByIdAsync(policy.OrganizationId);
                                    await organizationService.DeleteUserAsync(policy.OrganizationId, orgUser.Id,
                                        savingUserId);
                                    await _mailService.SendOrganizationUserRemovedForPolicyTwoStepEmailAsync(
                                        organization.Name, orgUser.Email);
                                }
                            }
                        break;
                        case Enums.PolicyType.SingleOrg:
                            var userOrgs = await _organizationUserRepository.GetManyByManyUsersAsync(
                                    removableOrgUsers.Select(ou => ou.UserId.Value));
                            organization = organization ?? await _organizationRepository.GetByIdAsync(policy.OrganizationId);
                            foreach (var orgUser in removableOrgUsers)
                            {
                                if (userOrgs.Any(ou => ou.UserId == orgUser.UserId 
                                            && ou.OrganizationId != organization.Id 
                                            && ou.Status != OrganizationUserStatusType.Invited))
                                {
                                    await organizationService.DeleteUserAsync(policy.OrganizationId, orgUser.Id,
                                        savingUserId);
                                    await _mailService.SendOrganizationUserRemovedForPolicySingleOrgEmailAsync(
                                        organization.Name, orgUser.Email);
                                }
                            }
                        break;
                        default:
                        break;
                    }
                }
            }
            policy.RevisionDate = DateTime.UtcNow;
            await _policyRepository.UpsertAsync(policy);
            await _eventService.LogPolicyEventAsync(policy, Enums.EventType.Policy_Updated);
        }
    }
}
