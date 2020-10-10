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
                    var kickableOffenses = new List<Enums.PolicyType> {
                        Enums.PolicyType.TwoFactorAuthentication,
                        Enums.PolicyType.OnlyOrg
                    };

                    if (kickableOffenses.Contains(currentPolicy.Type))
                    {
                        Organization organization = null;
                        var orgUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(
                            policy.OrganizationId);
                        foreach (var orgUser in orgUsers.Where(ou =>
                            ou.Status != Enums.OrganizationUserStatusType.Invited &&
                            ou.Type != Enums.OrganizationUserType.Owner && ou.UserId != savingUserId))
                        {
                            switch (currentPolicy.Type)
                            {
                                case Enums.PolicyType.TwoFactorAuthentication:
                                    if (!await userService.TwoFactorIsEnabledAsync(orgUser))
                                    {
                                        if (organization == null)
                                        {
                                            organization = await _organizationRepository.GetByIdAsync(policy.OrganizationId);
                                        }
                                        await organizationService.DeleteUserAsync(policy.OrganizationId, orgUser.Id,
                                            savingUserId);
                                        await _mailService.SendOrganizationUserRemovedForPolicyTwoStepEmailAsync(
                                            organization.Name, orgUser.Email);
                                    }
                                    break;
                                case Enums.PolicyType.OnlyOrg:
                                    if (orgUser.UserId.HasValue)
                                    {
                                        var userOrgs = await _organizationUserRepository.GetManyByUserAsync(orgUser.UserId.Value);
                                        if (userOrgs.Any(ou => ou.Status != OrganizationUserStatusType.Invited))
                                        {
                                            if (organization == null)
                                            {
                                                organization = await _organizationRepository.GetByIdAsync(policy.OrganizationId);
                                            }
                                            await organizationService.DeleteUserAsync(policy.OrganizationId, orgUser.Id,
                                                savingUserId);
                                            await _mailService.SendOrganizationUserRemovedForPolicyOnlyOrgEmailAsync(
                                                organization.Name, orgUser.Email);
                                        }
                                    }
                                break;
                            }
                        }
                    }
                }
            }
            policy.RevisionDate = DateTime.UtcNow;
            await _policyRepository.UpsertAsync(policy);
            await _eventService.LogPolicyEventAsync(policy, Enums.EventType.Policy_Updated);
        }
    }
}
