using Bit.Core.AccessPolicies;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AccessPolicies
{
    public class AccessPolicyResultTests
    {
        [Theory]
        [BitAutoData]
        public void AccessPolicyResult_Concatenates_Errors(AccessPolicyResult first, AccessPolicyResult second)
        {
            var result = first.And(second);

            Assert.Equal($"{first.BlockReason}\n{second.BlockReason}", result.BlockReason);
        }
    }
}
