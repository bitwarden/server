using AutoFixture.Xunit2;
using Bit.Core.Tokens;
using Xunit;

namespace Bit.Core.Test.Tokens;

public class TokenTests
{
    [Theory, AutoData]
    public void InitializeWithString_ReturnsString(string initString)
    {
        var token = new Token(initString);

        Assert.Equal(initString, token.ToString());
    }

    [Theory, AutoData]
    public void AddsPrefix(Token token, string prefix)
    {
        Assert.Equal($"{prefix}{token.ToString()}", token.WithPrefix(prefix).ToString());
    }

    [Theory, AutoData]
    public void RemovePrefix_WithPrefix_RemovesPrefix(string initString, string prefix)
    {
        var token = new Token(initString).WithPrefix(prefix);

        Assert.Equal(initString, token.RemovePrefix(prefix).ToString());
    }

    [Theory, AutoData]
    public void RemovePrefix_WithoutPrefix_Throws(Token token, string prefix)
    {
        var exception = Assert.Throws<BadTokenException>(() => token.RemovePrefix(prefix));

        Assert.Equal($"Expected prefix, {prefix}, was not present.", exception.Message);
    }
}
