using System;
using System.Collections.Generic;

namespace Bit.Core.Context
{
    public interface ICurrentContext
    {
        List<CurrentContentOrganization> Organizations { get; set; }

        bool ManagePolicies(Guid orgId);
    }
}
