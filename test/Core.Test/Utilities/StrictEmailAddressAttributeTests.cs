using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Utilities
{
    public class StrictEmailAttributeTests
    {
        [Theory]
        [InlineData("hello@world.com")]         // regular email address
        [InlineData("hello@world.planet.com")]  // subdomain
        [InlineData("hello+1@world.com")]       // alias
        [InlineData("hello.there@world.com")]   // period in local-part
        public void IsValid_ReturnsTrueWhenValid(string email)
        {
            var sut = new StrictEmailAddressAttribute();

            var actual = sut.IsValid(email);
            
            Assert.True(actual);
        }
        
        [Theory]
        [InlineData(null)]                      // null
        [InlineData("hello@world.com\t")]       // trailing tab char
        [InlineData("\thello@world.com")]       // leading tab char
        [InlineData("hel\tlo@world.com")]       // local-part tab char
        [InlineData("hello@world.com\b")]       // trailing backspace char
        [InlineData("\"   \"hello@world.com")]  // leading spaces in quotes
        [InlineData("hello@world.com\"    \"")] // trailing spaces in quotes
        [InlineData("hel\"   \"lo@world.com")]  // local-part spaces in quotes
        [InlineData("Hello World <hello@world.com>")]    // friendly from
        [InlineData("<hello@world.com>")]       // wrapped angle brackets
        [InlineData("hello(comment)there@world.com")]   // comment
        [InlineData("hello@world.com.")]        // trailing period
        [InlineData(".hello@world.com")]        // leading period
        [InlineData("hello@world.com;")]        // trailing semicolon
        [InlineData(";hello@world.com")]        // leading semicolon
        public void IsValid_ReturnsFalseWhenInvalid(string email)
        {
            var sut = new StrictEmailAddressAttribute();

            var actual = sut.IsValid(email);
            
            Assert.False(actual);
        }
    }
}
