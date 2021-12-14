using System;
using System.Linq;
using System.Threading.Tasks;

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

        public AccessPolicyResult And(AccessPolicyResult result)
        {
            return new AccessPolicyResult(
                Permit && result.Permit,
                ConcatBlockReason(result)
            );
        }

        public AccessPolicyResult And(Func<AccessPolicyResult> resultGenerator)
        {
            if (Permit)
            {
                return resultGenerator();
            }
            return new(Permit, BlockReason);
        }

        public async Task<AccessPolicyResult> AndAsync(Func<Task<AccessPolicyResult>> resultGenerator)
        {
            if (Permit)
            {
                return await resultGenerator();
            }
            return new(Permit, BlockReason);
        }

        private string ConcatBlockReason(AccessPolicyResult result)
        {
            return string.Join('\n', new[] {
                BlockReason,
                result.BlockReason
            }.Where(r => !string.IsNullOrWhiteSpace(r)));
        }
    }
}
