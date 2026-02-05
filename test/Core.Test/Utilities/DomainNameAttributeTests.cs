using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class DomainNameValidatorAttributeTests
{
    [Theory]
    [InlineData("example.com")]                     // basic domain
    [InlineData("sub.example.com")]                 // subdomain
    [InlineData("sub.sub2.example.com")]            // multiple subdomains
    [InlineData("example-dash.com")]                // domain with dash
    [InlineData("123example.com")]                  // domain starting with number
    [InlineData("example123.com")]                  // domain with numbers
    [InlineData("e.com")]                           // short domain
    [InlineData("very-long-subdomain-name.example.com")]  // long subdomain
    [InlineData("wörldé.com")]                      // unicode domain (IDN)
    public void IsValid_ReturnsTrueWhenValid(string domainName)
    {
        var sut = new DomainNameValidatorAttribute();

        var actual = sut.IsValid(domainName);

        Assert.True(actual);
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]   // XSS attempt
    [InlineData("example.com<script>")]             // XSS suffix
    [InlineData("<img src=x>")]                     // HTML tag
    [InlineData("example.com\t")]                   // trailing tab
    [InlineData("\texample.com")]                   // leading tab
    [InlineData("exam\tple.com")]                   // middle tab
    [InlineData("example.com\n")]                   // newline
    [InlineData("example.com\r")]                   // carriage return
    [InlineData("example.com\b")]                   // backspace
    [InlineData("exam ple.com")]                    // space in domain
    [InlineData("example.com ")]                    // trailing space (after trim, becomes valid, but with space it's invalid)
    [InlineData(" example.com")]                    // leading space (after trim, becomes valid, but with space it's invalid)
    [InlineData("example&.com")]                    // ampersand
    [InlineData("example'.com")]                    // single quote
    [InlineData("example\".com")]                   // double quote
    [InlineData(".example.com")]                    // starts with dot
    [InlineData("example.com.")]                    // ends with dot
    [InlineData("example..com")]                    // double dot
    [InlineData("-example.com")]                    // starts with dash
    [InlineData("example-.com")]                    // label ends with dash
    [InlineData("")]                                // empty string
    [InlineData("   ")]                             // whitespace only
    [InlineData("http://example.com")]              // URL scheme
    [InlineData("example.com/path")]                // path component
    [InlineData("user@example.com")]                // email format
    public void IsValid_ReturnsFalseWhenInvalid(string domainName)
    {
        var sut = new DomainNameValidatorAttribute();

        var actual = sut.IsValid(domainName);

        Assert.False(actual);
    }

    [Fact]
    public void IsValid_ReturnsTrueWhenNull()
    {
        var sut = new DomainNameValidatorAttribute();

        var actual = sut.IsValid(null);

        // Null validation should be handled by [Required] attribute
        Assert.True(actual);
    }

    [Fact]
    public void IsValid_ReturnsFalseWhenTooLong()
    {
        var sut = new DomainNameValidatorAttribute();
        // Create a domain name longer than 253 characters
        var longDomain = new string('a', 250) + ".com";

        var actual = sut.IsValid(longDomain);

        Assert.False(actual);
    }
}
