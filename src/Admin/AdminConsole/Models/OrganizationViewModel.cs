﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Vault.Entities;

namespace Bit.Admin.AdminConsole.Models;

public class OrganizationViewModel
{
    public OrganizationViewModel()
    {
    }

    public OrganizationViewModel(Organization org, Provider provider, IEnumerable<OrganizationConnection> connections,
        IEnumerable<OrganizationUserUserDetails> orgUsers, IEnumerable<Cipher> ciphers,
        IEnumerable<Collection> collections,
        IEnumerable<Group> groups, IEnumerable<Policy> policies, int secretsCount, int projectCount,
        int serviceAccountsCount,
        int occupiedSmSeatsCount)

    {
        Organization = org;
        Provider = provider;
        Connections = connections ?? Enumerable.Empty<OrganizationConnection>();
        HasPublicPrivateKeys = org.PublicKey != null && org.PrivateKey != null;
        UserInvitedCount = orgUsers.Count(u => u.Status == OrganizationUserStatusType.Invited);
        UserAcceptedCount = orgUsers.Count(u => u.Status == OrganizationUserStatusType.Accepted);
        UserConfirmedCount = orgUsers.Count(u => u.Status == OrganizationUserStatusType.Confirmed);
        OccupiedSeatCount = UserInvitedCount + UserAcceptedCount + UserConfirmedCount;
        CipherCount = ciphers.Count();
        CollectionCount = collections.Count();
        GroupCount = groups?.Count() ?? 0;
        PolicyCount = policies?.Count() ?? 0;
        var organizationUserStatus = org.Status == OrganizationStatusType.Pending
            ? OrganizationUserStatusType.Invited
            : OrganizationUserStatusType.Confirmed;
        Owners = string.Join(", ",
            orgUsers
                .Where(u => u.Type == OrganizationUserType.Owner && u.Status == organizationUserStatus)
                .Select(u => u.Email));
        Admins = string.Join(", ",
            orgUsers
                .Where(u => u.Type == OrganizationUserType.Admin && u.Status == organizationUserStatus)
                .Select(u => u.Email));
        SecretsCount = secretsCount;
        ProjectsCount = projectCount;
        ServiceAccountsCount = serviceAccountsCount;
        OccupiedSmSeatsCount = occupiedSmSeatsCount;
    }

    public Organization Organization { get; set; }
    public Provider Provider { get; set; }
    public IEnumerable<OrganizationConnection> Connections { get; set; }
    public string Owners { get; set; }
    public string Admins { get; set; }
    public int UserInvitedCount { get; set; }
    public int UserConfirmedCount { get; set; }
    public int UserAcceptedCount { get; set; }
    public int OccupiedSeatCount { get; set; }
    public int CipherCount { get; set; }
    public int CollectionCount { get; set; }
    public int GroupCount { get; set; }
    public int PolicyCount { get; set; }
    public bool HasPublicPrivateKeys { get; set; }
    public int SecretsCount { get; set; }
    public int ProjectsCount { get; set; }
    public int ServiceAccountsCount { get; set; }
    public int OccupiedSmSeatsCount { get; set; }
    public bool UseSecretsManager => Organization.UseSecretsManager;
    public bool UseRiskInsights => Organization.UseRiskInsights;
}
