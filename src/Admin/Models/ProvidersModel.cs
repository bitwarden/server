﻿using Bit.Core.Models.Table;
using Bit.Core.Models.Table.Provider;

namespace Bit.Admin.Models
{
    public class ProvidersModel : PagedModel<Provider>
    {
        public string Name { get; set; }
        public string UserEmail { get; set; }
        public bool? Paid { get; set; }
        public string Action { get; set; }
        public bool SelfHosted { get; set; }
    }
}
