using Bit.Core.Models.Table;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bit.Core.Models.Api
{
    public class UpdateLoginsRequestModel
    {
        public IEnumerable<string> DefaultLogins { get; set; }

        public User ToUser(User existingUser)
        {
            existingUser.DefaultLogins = DefaultLogins != null ? JsonConvert.SerializeObject(DefaultLogins) : null;
            return existingUser;
        }
    }
}
