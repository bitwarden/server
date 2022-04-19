using System;
namespace Bit.Core.Models.Data
{
    public class SsoToken
    {
        public const string TokenName = "ssoToken";

        public SsoToken()
        {
        }

        public Guid OrganizationId { get; set; }
        public string DomainHint { get; set; }
    }
}
