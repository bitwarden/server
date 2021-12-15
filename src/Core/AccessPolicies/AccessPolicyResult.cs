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

        /// <summary>
        /// Lazily evaluates AccessPolicyResults, terminating evaluation on the first occurrence of Permit == false
        /// </summary>
        /// <param name="resultGenerator"></param>
        /// <returns></returns>
        public AccessPolicyResult And(params Func<AccessPolicyResult>[] resultGenerators)
        {
            var currentResult = new AccessPolicyResult(Permit, BlockReason);

            foreach (var resultGenerator in resultGenerators)
            {
                if (!currentResult.Permit)
                {
                    return currentResult;
                }
                currentResult = currentResult.And(resultGenerator());
            }

            return currentResult;
        }

        /// <summary>
        /// Lazily evaluates AccessPolicyResults, terminating evaluation on the first occurrence of Permit == false
        /// </summary>
        /// <param name="resultGenerators"></param>
        /// <returns></returns>
        public async Task<AccessPolicyResult> AndAsync(params Func<Task<AccessPolicyResult>>[] resultGenerators)
        {
            var currentResult = new AccessPolicyResult(Permit, BlockReason);

            foreach (var resultGenerator in resultGenerators)
            {
                if (!currentResult.Permit)
                {
                    return currentResult;
                }

                currentResult = currentResult.And(await resultGenerator());
            }

            return currentResult;
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
