using Bit.Core.Models.Mail;

namespace Bit.Core.Auth.Models.Mail;

public class MasterPasswordHintViewModel : BaseMailModel
{
    public string Hint { get; set; }
}
