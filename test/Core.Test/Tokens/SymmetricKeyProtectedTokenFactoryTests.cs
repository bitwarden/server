using System;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Core.Test.Tokens
{
    [SutProviderCustomize]
    public class SymmetricKeyProtectedTokenFactoryTests
    {
        [Theory, BitAutoData]
        public void CanRoundTripTokenables(TestTokenable tokenable, string key,
            SutProvider<SymmetricKeyProtectedTokenFactory<TestTokenable>> sutProvider)
        {
            var token = sutProvider.Sut.Protect(key, tokenable);
            var recoveredTokenable = sutProvider.Sut.Unprotect(key, token);

            AssertHelper.AssertPropertyEqual(tokenable, recoveredTokenable);
        }

        [Theory, BitAutoData]
        public void PrependsClearText(TestTokenable tokenable, string key,
            SutProvider<SymmetricKeyProtectedTokenFactory<TestTokenable>> sutProvider)
        {
            var token = sutProvider.Sut.Protect(key, tokenable);

            Assert.StartsWith(sutProvider.GetDependency<string>("clearTextPrefix"), token);
        }

        [Theory, BitAutoData]
        public void EncryptsToken(TestTokenable tokenable, string key,
            SutProvider<SymmetricKeyProtectedTokenFactory<TestTokenable>> sutProvider)
        {
            var prefix = sutProvider.GetDependency<string>("clearTextPrefix");

            var token = sutProvider.Sut.Protect(key, tokenable);

            Assert.NotEqual(new Token(token).RemovePrefix(prefix), tokenable.ToToken());
        }

        [Theory, BitAutoData]
        public void ThrowsIfUnprotectFails(TestTokenable tokenable, string key1, string key2,
            SutProvider<SymmetricKeyProtectedTokenFactory<TestTokenable>> sutProvider)
        {
            var token = sutProvider.Sut.Protect(key1, tokenable);

            Assert.Throws<Exception>(() => sutProvider.Sut.Unprotect(key2, token));
        }

        [Theory, BitAutoData]
        public void TryUnprotect_FalseIfUnprotectFails(TestTokenable tokenable, string key1, string key2,
            SutProvider<SymmetricKeyProtectedTokenFactory<TestTokenable>> sutProvider)
        {
            var token = sutProvider.Sut.Protect(key1, tokenable);

            var result = sutProvider.Sut.TryUnprotect(key2, token, out var data);

            Assert.False(result);
            Assert.Null(data);
        }

        [Theory, BitAutoData]
        public void TokenValid_FalseIfUnprotectFails(TestTokenable tokenable, string key1, string key2,
            SutProvider<SymmetricKeyProtectedTokenFactory<TestTokenable>> sutProvider)
        {
            var token = sutProvider.Sut.Protect(key1, tokenable);

            var result = sutProvider.Sut.TokenValid(key2, token);

            Assert.False(result);
        }

        [Theory, BitAutoData]
        public void TokenValid_FalseIfTokenInvalid(TestTokenable tokenable, string key,
            SutProvider<SymmetricKeyProtectedTokenFactory<TestTokenable>> sutProvider)
        {
            tokenable.ForceInvalid = true;
            var token = sutProvider.Sut.Protect(key, tokenable);

            var result = sutProvider.Sut.TokenValid(key, token);

            Assert.False(result);
        }

        [Theory, BitAutoData]
        public void TryUnprotect_TrueIfSuccess(TestTokenable tokenable, string key,
            SutProvider<SymmetricKeyProtectedTokenFactory<TestTokenable>> sutProvider)
        {
            var token = sutProvider.Sut.Protect(key, tokenable);

            var result = sutProvider.Sut.TryUnprotect(key, token, out var data);

            Assert.True(result);
            AssertHelper.AssertPropertyEqual(tokenable, data);
        }

        [Theory, BitAutoData]
        public void TokenValid_TrueIfSuccess(TestTokenable tokenable, string key,
            SutProvider<SymmetricKeyProtectedTokenFactory<TestTokenable>> sutProvider)
        {
            var token = sutProvider.Sut.Protect(key, tokenable);

            var result = sutProvider.Sut.TokenValid(key, token);

            Assert.True(result);
        }

    }
}
