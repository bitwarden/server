// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Mail;

public class EmergencyAccessInvitedViewModel : BaseMailModel
{
    public string Name { get; set; }
    public string Id { get; set; }
    public string Email { get; set; }
    public string Token { get; set; }
    public string Url => $"{WebVaultUrl}/accept-emergency?id={Id}&name={Name}&email={Email}&token={Token}";
}
