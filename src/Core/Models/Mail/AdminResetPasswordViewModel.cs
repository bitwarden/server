namespace Bit.Core.Models.Mail;

public class AdminResetPasswordViewModel : BaseMailModel
{
    public string UserName { get; set; }
    public string OrgName { get; set; }
}
