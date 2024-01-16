using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Fido2NetLib;
using Xunit;

namespace Bit.Core.Test.Auth.Models.Business.Tokenables;

public class WebAuthnCredentialCreateOptionsTokenableTests
{
    [Theory, BitAutoData]
    public void Valid_TokenWithoutUser_ReturnsFalse(CredentialCreateOptions createOptions)
    {
        var token = new WebAuthnCredentialCreateOptionsTokenable(null, createOptions);

        var isValid = token.Valid;

        Assert.False(isValid);
    }

    [Theory, BitAutoData]
    public void Valid_TokenWithoutOptions_ReturnsFalse(User user)
    {
        var token = new WebAuthnCredentialCreateOptionsTokenable(user, null);

        var isValid = token.Valid;

        Assert.False(isValid);
    }

    [Theory, BitAutoData]
    public void Valid_NewlyCreatedToken_ReturnsTrue(User user, CredentialCreateOptions createOptions)
    {
        var token = new WebAuthnCredentialCreateOptionsTokenable(user, createOptions);

        var isValid = token.Valid;

        Assert.True(isValid);
    }

    [Theory, BitAutoData]
    public void ValidIsValid_TokenWithoutUser_ReturnsFalse(User user, CredentialCreateOptions createOptions)
    {
        var token = new WebAuthnCredentialCreateOptionsTokenable(null, createOptions);

        var isValid = token.TokenIsValid(user);

        Assert.False(isValid);
    }

    [Theory, BitAutoData]
    public void ValidIsValid_TokenWithoutOptions_ReturnsFalse(User user)
    {
        var token = new WebAuthnCredentialCreateOptionsTokenable(user, null);

        var isValid = token.TokenIsValid(user);

        Assert.False(isValid);
    }

    [Theory, BitAutoData]
    public void ValidIsValid_NonMatchingUsers_ReturnsFalse(User user1, User user2, CredentialCreateOptions createOptions)
    {
        var token = new WebAuthnCredentialCreateOptionsTokenable(user1, createOptions);

        var isValid = token.TokenIsValid(user2);

        Assert.False(isValid);
    }

    [Theory, BitAutoData]
    public void ValidIsValid_SameUser_ReturnsTrue(User user, CredentialCreateOptions createOptions)
    {
        var token = new WebAuthnCredentialCreateOptionsTokenable(user, createOptions);

        var isValid = token.TokenIsValid(user);

        Assert.True(isValid);
    }
}

