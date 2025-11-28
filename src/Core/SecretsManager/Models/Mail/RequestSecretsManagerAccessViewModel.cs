// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.Mail;

namespace Bit.Core.SecretsManager.Models.Mail;

public class RequestSecretsManagerAccessViewModel : BaseMailModel
{
    public string UserNameRequestingAccess { get; set; }
    public string OrgName { get; set; }
    public string EmailContent { get; set; }
}
