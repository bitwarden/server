using System.Net;
using Bit.Icons.Models;
using Xunit;

namespace Bit.Icons.Test.Models;

public class IconUriTests
{
    [Theory]
    [InlineData("https://test.local", "1.1.1.1", true)]
    [InlineData("https://test.local:4443", "1.1.1.1", false)] // Non standard port
    [InlineData("http://test", "1.1.1.1", false)] // top level domain
    [InlineData("https://test.local", "127.0.0.1", false)] // IP is internal
    [InlineData("https://test.local", "::1", false)] // IP is internal
    [InlineData("https://1.1.1.1", "::1", false)] // host is IP
    public void IsValid(string uri, string ip, bool expectedResult)
    {
        var result = new IconUri(new Uri(uri), IPAddress.Parse(ip)).IsValid;

        Assert.Equal(expectedResult, result);
    }
}
