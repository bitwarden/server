using Bit.Core.Utilities;
using Newtonsoft.Json;

namespace Bit.Core.Models.Mail
{
    public class OrganizationUserInvitedViewModel : BaseMailModel
    {
        [JsonConverter(typeof(EncodedStringConverter))]
        public string OrganizationName { get; set; }
        public string OrganizationId { get; set; }
        public string OrganizationUserId { get; set; }
        [JsonConverter(typeof(EncodedStringConverter))]
        public string Email { get; set; }
        [JsonConverter(typeof(EncodedStringConverter))]
        public string OrganizationNameUrlEncoded { get; set; }
        public string Token { get; set; }
        [JsonConverter(typeof(EncodedStringConverter))]
        public string Url => string.Format("{0}/accept-organization?organizationId={1}&" +
            "organizationUserId={2}&email={3}&organizationName={4}&token={5}",
            WebVaultUrl,
            OrganizationId,
            OrganizationUserId,
            Email,
            OrganizationNameUrlEncoded,
            Token);
    }
}
