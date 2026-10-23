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
        // The polymorphic reader reports a missing discriminator as NotSupportedException, not JsonException, and it
        // still has to become a rejection rather than a 500.
        var result = _sut.Validate("""[{"cidrs":["10.0.0.0/8"]}]""");

        Assert.False(result.IsValid);
        Assert.Contains("kind", result.Error);
    }

    [Fact]
    public void Validate_KindAfterTheOtherProperties_IsValid()
    {
        // Property order means nothing in JSON, and a serializer that sorts keys writes "cidrs" before "kind".
        var result = _sut.Validate("""[{"cidrs":["10.0.0.0/8"],"kind":"ip_allowlist"}]""");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_NullCidrsValue_IsInvalid()
    {
        // "cidrs": null deserialises, so the condition has to reject it rather than throw.
        var result = _sut.Validate("""[{"kind":"ip_allowlist","cidrs":null}]""");

        Assert.False(result.IsValid);
        Assert.Contains("at least one CIDR", result.Error);
    }

    [Fact]
    public void Validate_LegacyAllOfKind_IsInvalid()
    {
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
        // A conditionless rule is allowed; it auto-approves, routing access through the PAM flow for audit logging.
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
