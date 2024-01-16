using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Test.Common.AutoFixture.Attributes;
using Fido2NetLib;
using Xunit;

namespace Bit.Core.Test.Auth.Models.Business.Tokenables;

public class WebAuthnLoginAssertionOptionsTokenableTests
{
    [Theory, BitAutoData]
    public void Valid_TokenWithoutOptions_ReturnsFalse(WebAuthnLoginAssertionOptionsScope scope)
    {
        var token = new WebAuthnLoginAssertionOptionsTokenable(scope, null);

        var isValid = token.Valid;

        Assert.False(isValid);
    }

    [Theory, BitAutoData]
    public void Valid_NewlyCreatedToken_ReturnsTrue(WebAuthnLoginAssertionOptionsScope scope, AssertionOptions createOptions)
    {
        var token = new WebAuthnLoginAssertionOptionsTokenable(scope, createOptions);


        var isValid = token.Valid;

        Assert.True(isValid);
    }

    [Theory, BitAutoData]
    public void ValidIsValid_TokenWithoutOptions_ReturnsFalse(WebAuthnLoginAssertionOptionsScope scope)
    {
        var token = new WebAuthnLoginAssertionOptionsTokenable(scope, null);

        var isValid = token.TokenIsValid(scope);

        Assert.False(isValid);
    }

    [Theory, BitAutoData]
    public void ValidIsValid_NonMatchingScope_ReturnsFalse(WebAuthnLoginAssertionOptionsScope scope1, WebAuthnLoginAssertionOptionsScope scope2, AssertionOptions createOptions)
    {
        var token = new WebAuthnLoginAssertionOptionsTokenable(scope1, createOptions);

        var isValid = token.TokenIsValid(scope2);

        Assert.False(isValid);
    }

    [Theory, BitAutoData]
    public void ValidIsValid_SameScope_ReturnsTrue(WebAuthnLoginAssertionOptionsScope scope, AssertionOptions createOptions)
    {
        var token = new WebAuthnLoginAssertionOptionsTokenable(scope, createOptions);

        var isValid = token.TokenIsValid(scope);

        Assert.True(isValid);
    }
}
