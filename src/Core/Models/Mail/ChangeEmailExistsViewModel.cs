namespace Bit.Core.Models.Mail;

public class ChangeEmailExistsViewModel : BaseMailModel
{
    public string FromEmail { get; set; }
    public string ToEmail { get; set; }
}
