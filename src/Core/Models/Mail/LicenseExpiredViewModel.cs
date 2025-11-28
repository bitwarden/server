// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Mail;

public class LicenseExpiredViewModel : BaseMailModel
{
    public string OrganizationName { get; set; }
    public bool IsOrganization => !string.IsNullOrWhiteSpace(OrganizationName);
}
