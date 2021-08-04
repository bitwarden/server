using System;
using System.Collections.Generic;
using Bit.Core.Enums.Provider;
using Bit.Core.Models.Api;

namespace Bit.Core.Models.Business.Provider
{
    public class ProviderUserInvite<T>
    {
        public IEnumerable<T> UserIdentifiers { get; set; }
        public ProviderUserType Type { get; set; }
        public Guid InvitingUserId { get; set; }
        public Guid ProviderId { get; set; }
    }
}
