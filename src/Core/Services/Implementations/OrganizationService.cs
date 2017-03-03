using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Repositories;
using Bit.Core.Models.Business;
using Bit.Core.Domains;
using Bit.Core.Utilities;
using Bit.Core.Exceptions;

namespace Bit.Core.Services
{
    public class OrganizationService : IOrganizationService
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;

        public OrganizationService(
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository)
        {
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
        }

        public async Task<Tuple<Organization, OrganizationUser>> SignUpAsync(OrganizationSignup organizationSignup)
        {
            var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == organizationSignup.Plan);
            if(plan == null)
            {
                throw new BadRequestException("Plan not found.");
            }

            var organization = new Organization
            {
                Name = organizationSignup.Name,
                UserId = organizationSignup.Owner.Id,
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
                organization.PlanRenewalDate = DateTime.UtcNow.Add(plan.Cycle());
            }

            await _organizationRepository.CreateAsync(organization);

            try
            {
                var orgUser = new OrganizationUser
                {
                    OrganizationId = organization.Id,
                    UserId = organizationSignup.Owner.Id,
                    Email = organizationSignup.Owner.Email,
                    Key = organizationSignup.OwnerKey,
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
    }
}
