namespace Bit.Core.Models.Mail
{
    public class EmergencyAccessInvitedViewModel : BaseMailModel
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Email { get; set; }
        public string Token { get; set; }
        public string Url => string.Format("{0}/accept-emergency?id={1}&name={2}&email={3}&token={4}",
            WebVaultUrl,
            Id,
            Name,
            Email,
            Token);
    }
}
