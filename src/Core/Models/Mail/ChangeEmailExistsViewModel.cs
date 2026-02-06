// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Mail;

public class ChangeEmailExistsViewModel : BaseMailModel
{
    public string FromEmail { get; set; }
    public string ToEmail { get; set; }
}
