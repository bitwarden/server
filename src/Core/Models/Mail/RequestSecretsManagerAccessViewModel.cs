namespace Bit.Core.Models.Mail;

public class RequestSecretsManagerAccessViewModel : BaseMailModel
{
    public string UserNameRequestingAccess { get; set; }
    public string OrgName { get; set; }
    public string EmailContent { get; set; }
    public string TrySecretsManagerUrl { get; set; }
}
