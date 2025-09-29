using Bit.Core.AdminConsole.Models.Data;

namespace Bit.Core.Models.Data.Organizations.OrganizationUsers;

public class OrganizationUserOrganizationDetails : BaseUserOrganizationDetails
{
    public Guid OrganizationUserId { get; set; }
    public bool UseSecretsManager { get; set; }
    public Enums.OrganizationUserStatusType Status { get; set; }
    public Enums.OrganizationUserType Type { get; set; }
    public string? SsoExternalId { get; set; }
    public string? Permissions { get; set; }
    public string? ResetPasswordKey { get; set; }
    public string? FamilySponsorshipFriendlyName { get; set; }
    public DateTime? FamilySponsorshipLastSyncDate { get; set; }
    public DateTime? FamilySponsorshipValidUntil { get; set; }
    public bool? FamilySponsorshipToDelete { get; set; }
    public bool AccessSecretsManager { get; set; }
    public bool UsePasswordManager { get; set; }
    public int? SmSeats { get; set; }
    public int? SmServiceAccounts { get; set; }
    public bool? IsAdminInitiated { get; set; }
}
