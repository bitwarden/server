using Bit.Scim.Utilities;
using Xunit;

namespace Bit.Scim.Test.Utilities;

public class ScimFilterParserTests
{
    private static readonly Dictionary<string, Func<TestItem, string?>> _selectors = new()
    {
        ["username"] = i => i.UserName,
        ["externalid"] = i => i.ExternalId,
        ["displayname"] = i => i.DisplayName
    };

    [Theory]
    [InlineData("userName eq \"john\"")]
    [InlineData("userName ne \"john\"")]
    [InlineData("userName co \"john\"")]
    [InlineData("userName sw \"john\"")]
    [InlineData("externalId eq \"abc123\"")]
    [InlineData("displayName eq \"Test Group\"")]
    public void TryGetPredicate_ValidFilter_ReturnsPredicate(string filter)
    {
        var predicate = ScimFilterParser.TryGetPredicate(filter, _selectors);

        Assert.NotNull(predicate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalidfilter")]
    public void TryGetPredicate_InvalidFilter_ReturnsNull(string filter)
    {
        var predicate = ScimFilterParser.TryGetPredicate(filter, _selectors);

        Assert.Null(predicate);
    }

    [Fact]
    public void TryGetPredicate_NullFilter_ReturnsNull()
    {
        var predicate = ScimFilterParser.TryGetPredicate(null, _selectors);

        Assert.Null(predicate);
    }

    [Fact]
    public void TryGetPredicate_UnknownAttribute_ReturnsNull()
    {
        var predicate = ScimFilterParser.TryGetPredicate("unknownAttr eq \"value\"", _selectors);

        Assert.Null(predicate);
    }

    [Fact]
    public void TryGetPredicate_UnsupportedOperator_ReturnsNull()
    {
        var predicate = ScimFilterParser.TryGetPredicate("userName gt \"value\"", _selectors);

        Assert.Null(predicate);
    }

    [Fact]
    public void TryGetPredicate_Eq_MatchesExact()
    {
        var predicate = ScimFilterParser.TryGetPredicate("userName eq \"john\"", _selectors)!;

        Assert.True(predicate(new TestItem("john", null, null)));
        Assert.False(predicate(new TestItem("jane", null, null)));
    }

    [Fact]
    public void TryGetPredicate_Ne_MatchesNonEqual()
    {
        var predicate = ScimFilterParser.TryGetPredicate("userName ne \"john\"", _selectors)!;

        Assert.True(predicate(new TestItem("jane", null, null)));
        Assert.False(predicate(new TestItem("john", null, null)));
    }

    [Fact]
    public void TryGetPredicate_Co_MatchesSubstring()
    {
        var predicate = ScimFilterParser.TryGetPredicate("userName co \"world\"", _selectors)!;

        Assert.True(predicate(new TestItem("hello world", null, null)));
        Assert.False(predicate(new TestItem("hello", null, null)));
    }

    [Fact]
    public void TryGetPredicate_Sw_MatchesPrefix()
    {
        var predicate = ScimFilterParser.TryGetPredicate("userName sw \"hel\"", _selectors)!;

        Assert.True(predicate(new TestItem("hello", null, null)));
        Assert.False(predicate(new TestItem("world", null, null)));
    }

    [Fact]
    public void TryGetPredicate_CaseInsensitive()
    {
        var predicate = ScimFilterParser.TryGetPredicate("userName eq \"JOHN\"", _selectors)!;

        Assert.True(predicate(new TestItem("john", null, null)));
    }

    [Fact]
    public void TryGetPredicate_NullFieldValue_Eq_ReturnsFalse()
    {
        var predicate = ScimFilterParser.TryGetPredicate("userName eq \"value\"", _selectors)!;

        Assert.False(predicate(new TestItem(null, null, null)));
    }

    [Fact]
    public void TryGetPredicate_NullFieldValue_Ne_ReturnsTrue()
    {
        var predicate = ScimFilterParser.TryGetPredicate("userName ne \"value\"", _selectors)!;

        Assert.True(predicate(new TestItem(null, null, null)));
    }

    private record TestItem(string? UserName, string? ExternalId, string? DisplayName);
}
