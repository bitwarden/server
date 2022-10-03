using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

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

        string userEmail = null;
        if (_minimumStatus == OrganizationUserStatusType.Invited)
        {
            // Invited orgUsers do not have a UserId associated with them, so we have to match up their email
            userEmail = dbContext.Users.Find(_userId)?.Email;
        }

        var query = from p in dbContext.Policies
                    join ou in dbContext.OrganizationUsers
                        on p.OrganizationId equals ou.OrganizationId
                    where
                        ((_minimumStatus > OrganizationUserStatusType.Invited && ou.UserId == _userId) ||
                            (_minimumStatus == OrganizationUserStatusType.Invited && ou.Email == userEmail)) &&
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
