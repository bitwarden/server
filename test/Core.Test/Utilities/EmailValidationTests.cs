using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class EmailValidationTests
{
    [Theory]
    [InlineData("user@Example.COM", "example.com")]
    [InlineData("user@EXAMPLE.COM", "example.com")]
    [InlineData("user@example.com", "example.com")]
    [InlineData("user@Example.Com", "example.com")]
    [InlineData("User@DOMAIN.CO.UK", "domain.co.uk")]
    public void GetDomain_WithMixedCaseEmail_ReturnsLowercaseDomain(string email, string expectedDomain)
    {
        // Act
        var result = EmailValidation.GetDomain(email);

        // Assert
        Assert.Equal(expectedDomain, result);
    }

    [Theory]
    [InlineData("hello@world.com", "world.com")]                // regular email address
    [InlineData("hello@world.planet.com", "world.planet.com")]  // subdomain
    [InlineData("hello+1@world.com", "world.com")]              // alias
    [InlineData("hello.there@world.com", "world.com")]          // period in local-part
    [InlineData("hello@wörldé.com", "wörldé.com")]              // unicode domain
    [InlineData("hello@world.cömé", "world.cömé")]              // unicode top-level domain
    public void GetDomain_WithValidEmail_ReturnsLowercaseDomain(string email, string expectedDomain)
    {
        // Act
        var result = EmailValidation.GetDomain(email);

        // Assert
        Assert.Equal(expectedDomain, result);
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("")]
    public void GetDomain_WithInvalidEmail_ThrowsBadRequestException(string email)
    {
        // Act & Assert
        var exception = Assert.Throws<BadRequestException>(() => EmailValidation.GetDomain(email));
        Assert.Equal("Invalid email address format.", exception.Message);
    }
}
