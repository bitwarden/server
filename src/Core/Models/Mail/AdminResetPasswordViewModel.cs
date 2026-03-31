namespace Bit.Core.Models.Mail;

public class AdminResetPasswordViewModel : BaseMailModel
{
    public string? UserName { get; set; }
    public string? OrgName { get; set; }
    public bool ResetMasterPassword { get; set; }
    public bool ResetTwoFactor { get; set; }
}
