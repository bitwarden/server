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
    public void Validate_LegacyAllOfKind_IsInvalid()
    {
        // The flattened model dropped the all_of composite; a document that still nests one is rejected rather than
        // silently accepted.
        var result = _sut.Validate("""[{"kind":"all_of","conditions":[]}]""");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_TimeOfDay_UnknownWeekdayToken_IsInvalid()
    {
        // An unknown weekday token fails deserialization (via AccessWeekdayJsonConverter) before any condition-level
        // check runs, so the whole document is rejected as malformed. Per-condition rules are covered by the
        // condition tests (IpAllowlistConditionTests, TimeOfDayConditionTests).
        var result = _sut.Validate("""[{"kind":"time_of_day","tz":"UTC","windows":[{"days":["funday"],"from":"09:00","to":"17:00"}]}]""");

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
