using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Quartz.Util;

namespace Bit.Core.AccessPolicies
{
    public abstract class BaseAccessPolicies
    {
        protected Dictionary<string, AccessPolicyResult> PermissionOverrides { get; } = new();
        protected static AccessPolicyResult Success => new(true, "");
        protected static AccessPolicyResult Fail() => new(false, null);
        protected static AccessPolicyResult Fail(string reason) => new(false, reason);

        protected bool OverrideExists([CallerMemberName] string callerName = "") => PermissionOverrides.ContainsKey(callerName);
        protected AccessPolicyResult this[string callerName] => PermissionOverrides.TryGetAndReturn(callerName);
        public void OverridePermission(string permissionMethodName, AccessPolicyResult overrideValue)
        {
            PermissionOverrides[permissionMethodName] = overrideValue;
        }
    }
}
