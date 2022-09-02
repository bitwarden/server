using System.Security.Cryptography;
using AutoFixture;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace Bit.Core.Test.Tokens;

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

    [Theory, BitAutoData]
    public void ThrowsIfUnprotectFails(TestTokenable tokenable)
    {
        var sutProvider = GetSutProvider();

        var token = sutProvider.Sut.Protect(tokenable);
        token += "stuff to make sure decryption fails";

        Assert.Throws<CryptographicException>(() => sutProvider.Sut.Unprotect(token));
    }

    [Theory, BitAutoData]
    public void TryUnprotect_FalseIfUnprotectFails(TestTokenable tokenable)
    {
        var sutProvider = GetSutProvider();
        var token = sutProvider.Sut.Protect(tokenable) + "fail decryption";

        var result = sutProvider.Sut.TryUnprotect(token, out var data);

        Assert.False(result);
        Assert.Null(data);
    }

    [Theory, BitAutoData]
    public void TokenValid_FalseIfUnprotectFails(TestTokenable tokenable)
    {
        var sutProvider = GetSutProvider();
        var token = sutProvider.Sut.Protect(tokenable) + "fail decryption";

        var result = sutProvider.Sut.TokenValid(token);

        Assert.False(result);
    }


    [Theory, BitAutoData]
    public void TokenValid_FalseIfTokenInvalid(TestTokenable tokenable)
    {
        var sutProvider = GetSutProvider();

        tokenable.ForceInvalid = true;
        var token = sutProvider.Sut.Protect(tokenable);

        var result = sutProvider.Sut.TokenValid(token);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public void TryUnprotect_TrueIfSuccess(TestTokenable tokenable)
    {
        var sutProvider = GetSutProvider();
        var token = sutProvider.Sut.Protect(tokenable);

        var result = sutProvider.Sut.TryUnprotect(token, out var data);

        Assert.True(result);
        AssertHelper.AssertPropertyEqual(tokenable, data);
    }

    [Theory, BitAutoData]
    public void TokenValid_TrueIfSuccess(TestTokenable tokenable)
    {
        tokenable.ForceInvalid = false;
        var sutProvider = GetSutProvider();
        var token = sutProvider.Sut.Protect(tokenable);

        var result = sutProvider.Sut.TokenValid(token);

        Assert.True(result);
    }

}
