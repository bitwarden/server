namespace Bit.Core.Models.Mail;

public class LicenseExpiredViewModel : BaseMailModel
{
    public string OrganizationName { get; set; }
    public bool IsOrganization => !string.IsNullOrWhiteSpace(OrganizationName);
}
