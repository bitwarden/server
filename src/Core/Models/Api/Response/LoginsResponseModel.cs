using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bit.Core.Models.Api.Response
{
    public class LoginsResponseModel : ResponseModel
    {
        public LoginsResponseModel(User user) : base("logins")
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            DefaultLogins = user.DefaultLogins != null ?
                JsonConvert.DeserializeObject<List<string>>(user.DefaultLogins) : null;
        }

        public IEnumerable<string> DefaultLogins { get; set; }
    }
}
