namespace Bit.Core.AccessPolicies
{
    public class AccessPolicyResult
    {
        public bool Permit { get; private set; }
        public string BlockReason { get; private set; }

        public AccessPolicyResult(bool permit, string blockReason)
        {
            Permit = permit;
            BlockReason = blockReason;
        }
    }
}
