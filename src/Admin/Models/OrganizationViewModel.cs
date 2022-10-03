using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Admin.Models;

public class OrganizationViewModel
{
    public OrganizationViewModel() { }

    public OrganizationViewModel(Organization org, IEnumerable<OrganizationConnection> connections,
        IEnumerable<OrganizationUserUserDetails> orgUsers, IEnumerable<Cipher> ciphers, IEnumerable<Collection> collections,
        IEnumerable<Group> groups, IEnumerable<Policy> policies)
    {
        Organization = org;
        Connections = connections ?? Enumerable.Empty<OrganizationConnection>();
        HasPublicPrivateKeys = org.PublicKey != null && org.PrivateKey != null;
        UserInvitedCount = orgUsers.Count(u => u.Status == OrganizationUserStatusType.Invited);
        UserAcceptedCount = orgUsers.Count(u => u.Status == OrganizationUserStatusType.Accepted);
        UserConfirmedCount = orgUsers.Count(u => u.Status == OrganizationUserStatusType.Confirmed);
        OccupiedSeatCount = orgUsers.Count(u => u.OccupiesOrganizationSeat);
        CipherCount = ciphers.Count();
        CollectionCount = collections.Count();
        GroupCount = groups?.Count() ?? 0;
        PolicyCount = policies?.Count() ?? 0;
        Owners = string.Join(", ",
            orgUsers
            .Where(u => u.Type == OrganizationUserType.Owner && u.Status == OrganizationUserStatusType.Confirmed)
            .Select(u => u.Email));
        Admins = string.Join(", ",
            orgUsers
            .Where(u => u.Type == OrganizationUserType.Admin && u.Status == OrganizationUserStatusType.Confirmed)
            .Select(u => u.Email));
    }

    public Organization Organization { get; set; }
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
}
