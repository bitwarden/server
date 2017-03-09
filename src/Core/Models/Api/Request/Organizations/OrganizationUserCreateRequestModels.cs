using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class OrganizationUserInviteRequestModel
    {
        public string Email { get; set; }
    }

    public class OrganizationUserAcceptRequestModel
    {
        public string Token { get; set; }
    }

    public class OrganizationUserConfirmRequestModel
    {
        public string Key { get; set; }
    }
}
