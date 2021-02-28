using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bit.Core.Models.Api.Response
{
    public class UsernamesResponseModel : ResponseModel
    {
        public UsernamesResponseModel(User user) : base("usernames")
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            DefaultUsernames = user.DefaultUsernames != null ?
                JsonConvert.DeserializeObject<List<string>>(user.DefaultUsernames) : null;
        }

        public IEnumerable<string> DefaultUsernames { get; set; }
    }
}
