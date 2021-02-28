using Bit.Core.Models.Table;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bit.Core.Models.Api
{
    public class UpdateUsernamesRequestModel
    {
        public IEnumerable<string> DefaultUsernames { get; set; }

        public User ToUser(User existingUser)
        {
            existingUser.DefaultUsernames = DefaultUsernames != null ? JsonConvert.SerializeObject(DefaultUsernames) : null;
            return existingUser;
        }
    }
}
