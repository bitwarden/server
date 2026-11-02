using Bit.Services.Pam.Services;
using Xunit;

namespace Bit.Services.Pam.Test.Services;

public class AccessRuleValidatorTests
{
    private readonly AccessRuleValidator _sut = new();

    [Fact]
    public void Validate_NullConditions_IsValid()
    {
        var result = _sut.Validate(null);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyOrWhitespaceConditions_IsInvalid(string conditionsJson)
    {
        var result = _sut.Validate(conditionsJson);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_MalformedJson_IsInvalid()
    {
        var result = _sut.Validate("{not json");

        Assert.False(result.IsValid);
        Assert.Contains("malformed", result.Error);
    }

    [Fact]
    public void Validate_NonArrayDocument_IsInvalid()
    {
        // The conditions document is a flat array; a bare object is rejected.
        var result = _sut.Validate("""{"kind":"human_approval"}""");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_UnknownKind_IsInvalid()
    {
        var result = _sut.Validate("""[{"kind":"bogus"}]""");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_MissingKind_IsInvalid()
    {
        // A condition object with no discriminator at all cannot be mapped to a kind. The polymorphic reader reports
        // that as NotSupportedException rather than JsonException, so it needs its own catch — otherwise this answers
        // with an unhandled exception (a 500) instead of the actionable rejection below.
        var result = _sut.Validate("""[{"cidrs":["10.0.0.0/8"]}]""");

        Assert.False(result.IsValid);
        Assert.Contains("kind", result.Error);
    }

    [Fact]
    public void Validate_KindAfterTheOtherProperties_IsValid()
    {
        // Property order carries no meaning in JSON, so a document that writes "kind" last is legitimate — anything
        // that canonicalises keys alphabetically emits exactly this, since "cidrs" sorts before "kind".
        var result = _sut.Validate("""[{"cidrs":["10.0.0.0/8"],"kind":"ip_allowlist"}]""");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_NullCidrsValue_IsInvalid()
    {
        // "cidrs": null deserialises as a null value, so the condition has to reject it rather than throw a
        // NullReferenceException past this validator.
        var result = _sut.Validate("""[{"kind":"ip_allowlist","cidrs":null}]""");

        Assert.False(result.IsValid);
        Assert.Contains("at least one CIDR", result.Error);
    }

    [Fact]
    public void Validate_LegacyAllOfKind_IsInvalid()
    {
        // The flattened model dropped the all_of composite; a document that still nests one is rejected rather than
        // silently accepted.
        var result = _sut.Validate("""[{"kind":"all_of","conditions":[]}]""");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_MultipleConditions_IsValid()
    {
        var result = _sut.Validate("""
            [
              { "kind": "human_approval" },
              { "kind": "ip_allowlist", "cidrs": ["10.0.0.0/8"] }
            ]
            """);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyConditions_IsValid()
    {
        // A rule with no conditions is allowed: it gates nothing and exists to route access through the PAM flow
        // for audit logging.
        var result = _sut.Validate("[]");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ExceedsMaxConditions_IsInvalid()
    {
        var conditions = string.Join(",", Enumerable.Repeat("""{"kind":"human_approval"}""", 11));
        var result = _sut.Validate($$"""[{{conditions}}]""");

        Assert.False(result.IsValid);
        Assert.Contains("more than", result.Error);
    }

    [Fact]
    public void Validate_InvalidCondition_IsInvalid()
    {
        var result = _sut.Validate("""
            [
              { "kind": "human_approval" },
              { "kind": "ip_allowlist", "cidrs": ["bogus"] }
            ]
            """);

        Assert.False(result.IsValid);
        Assert.Contains("CIDR", result.Error);
    }
}
