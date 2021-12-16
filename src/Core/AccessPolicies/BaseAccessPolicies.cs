using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Quartz.Util;

namespace Bit.Core.AccessPolicies
{
    public abstract class BaseAccessPolicies
    {
        protected static AccessPolicyResult Success => new(true, "");
        protected static AccessPolicyResult Fail() => new(false, null);
        protected static AccessPolicyResult Fail(string reason) => new(false, reason);
    }
}
