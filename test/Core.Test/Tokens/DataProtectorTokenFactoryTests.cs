using AutoFixture;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace Bit.Core.Test.Tokens
{
    [SutProviderCustomize]
    public class DataProtectorTokenFactoryTests
    {
        public static SutProvider<DataProtectorTokenFactory<TestTokenable>> GetSutProvider()
        {
            var fixture = new Fixture();
            return new SutProvider<DataProtectorTokenFactory<TestTokenable>>(fixture)
                .SetDependency<IDataProtectionProvider>(fixture.Create<EphemeralDataProtectionProvider>())
                .Create();
        }

        [Theory, BitAutoData]
        public void CanRoundTripTokenables(TestTokenable tokenable)
        {
            var sutProvider = GetSutProvider();

            var token = sutProvider.Sut.Protect(tokenable);
            var recoveredTokenable = sutProvider.Sut.Unprotect(token);

            AssertHelper.AssertPropertyEqual(tokenable, recoveredTokenable);
        }

        [Theory, BitAutoData]
        public void PrependsClearText(TestTokenable tokenable)
        {
            var sutProvider = GetSutProvider();

            var token = sutProvider.Sut.Protect(tokenable);

            Assert.StartsWith(sutProvider.GetDependency<string>("clearTextPrefix"), token);
        }

        [Theory, BitAutoData]
        public void EncryptsToken(TestTokenable tokenable)
        {
            var sutProvider = GetSutProvider();
            var prefix = sutProvider.GetDependency<string>("clearTextPrefix");

            var token = sutProvider.Sut.Protect(tokenable);

            Assert.NotEqual(new Token(token).RemovePrefix(prefix), tokenable.ToToken());
        }
    }
}
