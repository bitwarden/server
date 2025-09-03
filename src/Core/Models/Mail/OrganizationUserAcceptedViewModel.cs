// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Mail;

public class OrganizationUserAcceptedViewModel : BaseMailModel
{
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; }
    public string UserIdentifier { get; set; }
    public string ConfirmUrl => $"{WebVaultUrl}/organizations/{OrganizationId}/members";
}
