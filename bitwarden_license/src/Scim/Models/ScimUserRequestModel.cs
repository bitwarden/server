using System;

namespace Bit.Scim.Models
{
    public class ScimUserRequestModel : BaseScimUserModel
    {
        public ScimUserRequestModel()
            : base(false)
        { }

        public string Password { get; set; }
    }
}
