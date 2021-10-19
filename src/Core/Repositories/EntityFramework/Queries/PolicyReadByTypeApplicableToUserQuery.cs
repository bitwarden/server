using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models.EntityFramework;
using System;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class PolicyReadByTypeApplicableToUserQuery : IQuery<Policy>
    {
        private readonly Guid _userId;
        private readonly PolicyType _policyType;
        private readonly OrganizationUserStatusType _minimumStatus;

        public PolicyReadByTypeApplicableToUserQuery(Guid userId, PolicyType policyType, OrganizationUserStatusType minimumStatus)
        {
            _userId = userId;
            _policyType = policyType;
            _minimumStatus = minimumStatus;
        }

        public IQueryable<Policy> Run(DatabaseContext dbContext)
        {
            var providerOrganizations = from pu in dbContext.ProviderUsers
                where pu.UserId == _userId
                join po in dbContext.ProviderOrganizations
                    on pu.ProviderId equals po.ProviderId
                select po;

            var query = from p in dbContext.Policies
                join ou in dbContext.OrganizationUsers
                    on p.OrganizationId equals ou.OrganizationId
                where ou.UserId == _userId &&
                    p.Type == _policyType &&
                    p.Enabled &&
                    ou.Status >= _minimumStatus &&
                    ou.Type >= OrganizationUserType.User &&
                    (ou.Permissions == null ||
                        ou.Permissions.Contains($"\"managePolicies\":false")) &&
                    !providerOrganizations.Any(po => po.OrganizationId == p.OrganizationId)
                select p;
            return query;
        }
    }
}
