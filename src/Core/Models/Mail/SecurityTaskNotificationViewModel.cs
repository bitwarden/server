namespace Bit.Core.Models.Mail;

public class SecurityTaskNotificationViewModel : BaseMailModel
{
    public string OrgName { get; set; }

    public int TaskCount { get; set; }

    public bool TaskCountPlural => TaskCount != 1;

    public string ReviewPasswordsUrl => $"{WebVaultUrl}/browser-extension-prompt";
}
