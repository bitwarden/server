// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Mail;

public class AdminResetPasswordViewModel : BaseMailModel
{
    public string UserName { get; set; }
    public string OrgName { get; set; }
}
