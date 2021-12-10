using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;

namespace Bit.Core.Services.OrganizationServices.UserInvite
{
    public class OrganizationUserInviteService : IOrganizationUserInviteService
    {
        readonly IOrganizationUserRepository _organizationUserRepository;

        public OrganizationUserInviteService(IOrganizationUserRepository organizationUserRepository)
        {
            _organizationUserRepository = organizationUserRepository;
        }

        private static List<PlannedOrganizationUser> GeneratePlannedOrganizationUsers(Organization organization,
            IEnumerable<(OrganizationUserInvite invite, string externalId)> invites,
            HashSet<string> existingUserEmails = null)
        {
            var orgUserInvites = new List<PlannedOrganizationUser>();
            foreach (var (invite, externalId) in invites)
            {
                foreach (var email in invite.Emails)
                {
                    // Make sure user is not already invited
                    // TODO: extract existing email to method
                    if (existingUserEmails.Contains(email))
                    {
                        continue;
                    }

                    var orgUser = new OrganizationUser
                    {
                        OrganizationId = organization.Id,
                        UserId = null,
                        Email = email.ToLowerInvariant(),
                        Key = null,
                        Type = invite.Type.Value,
                        Status = OrganizationUserStatusType.Invited,
                        AccessAll = invite.AccessAll,
                        ExternalId = externalId,
                        CreationDate = DateTime.UtcNow,
                        RevisionDate = DateTime.UtcNow,
                        Permissions = invite.Permissions?.ToString()
                    };

                    if (!orgUser.AccessAll && invite.Collections.Any())
                    {
                        orgUserInvites.Add(new LimitedCollectionsPlannedOrganizationUser(orgUser, invite.Collections));
                    }
                    else
                    {
                        orgUserInvites.Add(new AllCollectionsPlannedOrganizationUser(orgUser));
                    }
                }
            }

            return orgUserInvites;
        }

        public async Task<List<OrganizationUser>> InviteUsersAsync(Organization organization,
            IEnumerable<(OrganizationUserInvite invite, string externalId)> invites,
            HashSet<string> existingUserEmails = null)
        {
            if (existingUserEmails == null)
            {
                var existingEmails = new HashSet<string>(await _organizationUserRepository.SelectKnownEmailsAsync(
                    organization.Id, invites.SelectMany(i => i.invite.Emails), false), StringComparer.InvariantCultureIgnoreCase);
            }

            var plannedOrgUsers = GeneratePlannedOrganizationUsers(organization, invites, existingUserEmails);

            // Add users
            var prorationDate = DateTime.UtcNow;
            try
            {
                await _organizationUserRepository.CreateManyAsync(plannedOrgUsers
                    .Where(p => p is AllCollectionsPlannedOrganizationUser)
                    .Select(p => p.OrganizationUser));

                var limitedColletionPlans = plannedOrgUsers
                    .Where(p => p is LimitedCollectionsPlannedOrganizationUser)
                    .Select(p => p as LimitedCollectionsPlannedOrganizationUser);

                foreach (var plan in limitedColletionPlans)
                {
                    await _organizationUserRepository.CreateAsync(plan.OrganizationUser, plan.Collections);
                }
            }
            catch
            {
                // Revert any added users.
                await _organizationUserRepository.DeleteManyAsync(plannedOrgUsers.Select(p =>
                    p.OrganizationUser.Id).Where(id => id != default));

                throw;
            }

            return plannedOrgUsers.Select(p => p.OrganizationUser).ToList();
        }
    }

    public abstract class PlannedOrganizationUser
    {
        public OrganizationUser OrganizationUser { get; private set; }

        public PlannedOrganizationUser(OrganizationUser organizationUser)
        {
            OrganizationUser = organizationUser;
        }
    }

    public class AllCollectionsPlannedOrganizationUser : PlannedOrganizationUser
    {
        public AllCollectionsPlannedOrganizationUser(OrganizationUser organizationUser) : base(organizationUser) { }
    }

    public class LimitedCollectionsPlannedOrganizationUser : PlannedOrganizationUser
    {
        public IEnumerable<SelectionReadOnly> Collections { get; private set; }

        public LimitedCollectionsPlannedOrganizationUser(OrganizationUser organizationUser, IEnumerable<SelectionReadOnly> collections) :
            base(organizationUser)
        {
            Collections = collections;
        }
    }
}
