using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Test.Common.AutoFixture.Attributes;
using Stripe;
using Xunit;

namespace Bit.Core.Test.AccessPolicies
{
    public class AccessPolicyResultTests
    {
        [Fact]
        public void Concatenates_Errors()
        {
            var result = AccessPolicyResult.Fail("first").And(AccessPolicyResult.Fail("second"));

            Assert.Equal($"first\nsecond", result.BlockReason);
        }

        [Fact]
        public void LazyAnd_SingleError()
        {
            var result = AccessPolicyResult.Fail("first").LazyAnd(AccessPolicyResult.Fail("second"));
            Assert.Equal(AccessPolicyResult.Fail("first"), result);
        }

        [Fact]
        public void AndFails()
        {
            var result = AccessPolicyResult.Success.And(AccessPolicyResult.Fail("second"));
            Assert.Equal(AccessPolicyResult.Fail("second"), result);
        }

        public void FuncAndOverload_IsLazy()
        {
            var secondPolicyRan = false;
            var result = AccessPolicyResult.Fail("first").And(() =>
            {
                secondPolicyRan = true;
                return AccessPolicyResult.Success;
            });

            Assert.Equal(AccessPolicyResult.Fail("first"), result);
            Assert.False(secondPolicyRan);
        }

        [Fact]
        public async Task AndAsync_IsLazy()
        {
            var secondPolicyRan = false;
            var result = await AccessPolicyResult.Fail("first").AndAsync(() =>
            {
                secondPolicyRan = true;
                return Task.FromResult(AccessPolicyResult.Success);
            });

            Assert.Equal(AccessPolicyResult.Fail("first"), result);
            Assert.False(secondPolicyRan);
        }
    }
}
