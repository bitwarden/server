using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bit.Core.Models.Api.Response
{
    public class UsernamesResponseModel : ResponseModel
    {
        public UsernamesResponseModel(User user) : base("logins")
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            DefaultLogins = user.DefaultUsernames != null ?
                JsonConvert.DeserializeObject<List<string>>(user.DefaultUsernames) : null;
        }

        public IEnumerable<string> DefaultLogins { get; set; }
    }
}
