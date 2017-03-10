using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Repositories;
using Bit.Core.Models.Business;
using Bit.Core.Models.Table;
using Bit.Core.Utilities;
using Bit.Core.Exceptions;
using System.Collections.Generic;

namespace Bit.Core.Services
{
    public class OrganizationService : IOrganizationService
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ISubvaultRepository _subvaultRepository;
        private readonly ISubvaultUserRepository _subvaultUserRepository;
        private readonly IUserRepository _userRepository;

        public OrganizationService(
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            ISubvaultRepository subvaultRepository,
            ISubvaultUserRepository subvaultUserRepository,
            IUserRepository userRepository)
        {
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _subvaultRepository = subvaultRepository;
            _subvaultUserRepository = subvaultUserRepository;
            _userRepository = userRepository;
        }

        public async Task<Tuple<Organization, OrganizationUser>> SignUpAsync(OrganizationSignup signup)
        {
            var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == signup.Plan);
            if(plan == null)
            {
                throw new BadRequestException("Plan not found.");
            }

            var organization = new Organization
            {
                Name = signup.Name,
                UserId = signup.Owner.Id,
                PlanType = plan.Type,
                MaxUsers = plan.MaxUsers,
                PlanTrial = plan.Trial.HasValue,
                PlanPrice = plan.Trial.HasValue ? 0 : plan.Price,
                PlanRenewalPrice = plan.Price,
                Plan = plan.ToString(),
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow
            };

            if(plan.Trial.HasValue)
            {
                organization.PlanRenewalDate = DateTime.UtcNow.Add(plan.Trial.Value);
            }
            else if(plan.Cycle != null)
            {
                organization.PlanRenewalDate = DateTime.UtcNow.Add(plan.Cycle(DateTime.UtcNow));
            }

            await _organizationRepository.CreateAsync(organization);

            try
            {
                var orgUser = new OrganizationUser
                {
                    OrganizationId = organization.Id,
                    UserId = signup.Owner.Id,
                    Email = signup.Owner.Email,
                    Key = signup.OwnerKey,
                    Type = Enums.OrganizationUserType.Owner,
                    Status = Enums.OrganizationUserStatusType.Confirmed,
                    CreationDate = DateTime.UtcNow,
                    RevisionDate = DateTime.UtcNow
                };

                await _organizationUserRepository.CreateAsync(orgUser);

                return new Tuple<Organization, OrganizationUser>(organization, orgUser);
            }
            catch
            {
                await _organizationRepository.DeleteAsync(organization);
                throw;
            }
        }

        public async Task<OrganizationUser> InviteUserAsync(Guid organizationId, string email)
        {
            var orgUser = new OrganizationUser
            {
                OrganizationId = organizationId,
                UserId = null,
                Email = email,
                Key = null,
                Type = Enums.OrganizationUserType.User,
                Status = Enums.OrganizationUserStatusType.Invited,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow
            };

            await _organizationUserRepository.CreateAsync(orgUser);

            // TODO: send email

            return orgUser;
        }

        public async Task<OrganizationUser> AcceptUserAsync(Guid organizationUserId, User user, string token)
        {
            var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
            if(orgUser.Email != user.Email)
            {
                throw new BadRequestException("User invalid.");
            }

            // TODO: validate token

            orgUser.Status = Enums.OrganizationUserStatusType.Accepted;
            orgUser.UserId = orgUser.Id;
            orgUser.Email = null;
            await _organizationUserRepository.ReplaceAsync(orgUser);

            // TODO: send email

            return orgUser;
        }

        public async Task<OrganizationUser> ConfirmUserAsync(Guid organizationUserId, string key)
        {
            var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
            if(orgUser.Status != Enums.OrganizationUserStatusType.Accepted)
            {
                throw new BadRequestException("User not accepted.");
            }

            orgUser.Status = Enums.OrganizationUserStatusType.Confirmed;
            orgUser.Key = key;
            orgUser.Email = null;
            await _organizationUserRepository.ReplaceAsync(orgUser);

            // TODO: send email

            return orgUser;
        }

        public async Task<OrganizationUser> SaveUserAsync(OrganizationUser user, IEnumerable<SubvaultUser> subvaults)
        {
            if(user.Id.Equals(default(Guid)))
            {
                throw new BadRequestException("Invite the user first.");
            }

            await _organizationUserRepository.ReplaceAsync(user);

            var orgSubvaults = await _subvaultRepository.GetManyByOrganizationIdAsync(user.OrganizationId);
            var currentUserSubvaults = await _subvaultUserRepository.GetManyByOrganizationUserIdAsync(user.Id);

            // Let's make sure all these belong to this user and organization.
            var filteredSubvaults = subvaults.Where(s =>
                orgSubvaults.Any(os => os.Id == s.SubvaultId) &&
                (s.Id == default(Guid) || currentUserSubvaults.Any(cs => cs.Id == s.Id)));

            var subvaultsToDelete = currentUserSubvaults.Where(cs => !subvaults.Any(s => s.Id == cs.Id));

            foreach(var subvault in filteredSubvaults)
            {
                await _subvaultUserRepository.UpsertAsync(subvault);
            }

            foreach(var subvault in subvaultsToDelete)
            {
                await _subvaultUserRepository.DeleteAsync(subvault);
            }

            return user;
        }
    }
}
