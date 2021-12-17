using System;
using Bit.Core.Enums;
using Bit.Core.Enums.Provider;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Models.Table.Provider;
using Bit.Core.Utilities;

namespace Bit.Core.Context
{
    public class CurrentContentProvider
    {
        public CurrentContentProvider() { }

        public CurrentContentProvider(ProviderUser providerUser)
        {
            Id = providerUser.ProviderId;
            Type = providerUser.Type;
            Permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(providerUser.Permissions);
        }

        public Guid Id { get; set; }
        public ProviderUserType Type { get; set; }
        public Permissions Permissions { get; set; }
    }
}
