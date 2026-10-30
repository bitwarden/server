using System.Net;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Models.Conditions;
using Xunit;

namespace Bit.Services.Pam.Test.Models.Conditions;

public class IpAllowlistConditionTests
{
    // This condition ignores the timestamp, so the instant only has to be deterministic.
    private static AccessSignals Signals(IPAddress? ip) => new()
    {
        IpAddress = ip,
        Timestamp = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public void Evaluate_IpInRange_Allows()
    {
        var condition = new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] };

        var evaluation = condition.Evaluate(Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(AccessEvaluationOutcome.Allow, evaluation.Outcome);
    }

    [Fact]
    public void Evaluate_IpOutOfRange_Denies()
    {
        var condition = new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] };

        var evaluation = condition.Evaluate(Signals(IPAddress.Parse("192.168.1.1")));

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinIpRange, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_UnknownIp_DeniesClosed()
    {
        var condition = new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] };

        var evaluation = condition.Evaluate(Signals(ip: null));

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinIpRange, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_NoEntries_DeniesClosed()
    {
        var evaluation = new IpAllowlistCondition().Evaluate(Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinIpRange, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_NullCidrs_DeniesClosed()
    {
        // The property coalesces a JSON "cidrs": null to empty, without which this would throw.
        var evaluation = new IpAllowlistCondition { Cidrs = null! }.Evaluate(Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinIpRange, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_MalformedCidr_DeniesClosed()
    {
        var condition = new IpAllowlistCondition { Cidrs = ["not-a-cidr"] };

        var evaluation = condition.Evaluate(Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinIpRange, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_LaterCidrMatches_Allows()
    {
        var condition = new IpAllowlistCondition { Cidrs = ["192.168.0.0/16", "10.0.0.0/8"] };

        var evaluation = condition.Evaluate(Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(AccessEvaluationOutcome.Allow, evaluation.Outcome);
    }

    [Fact]
    public void Validate_NoCidrs_IsInvalid()
    {
        var result = new IpAllowlistCondition().Validate();

        Assert.False(result.IsValid);
        Assert.Contains("at least one CIDR", result.Error);
    }

    [Fact]
    public void Validate_NullCidrs_IsInvalid()
    {
        var result = new IpAllowlistCondition { Cidrs = null! }.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("at least one CIDR", result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-cidr")]
    [InlineData("10.0.0.0/99")]
    public void Validate_InvalidCidr_IsInvalid(string cidr)
    {
        var result = new IpAllowlistCondition { Cidrs = [cidr] }.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("Invalid CIDR", result.Error);
    }

    [Fact]
    public void Validate_NullCidr_IsInvalid()
    {
        // A JSON null becomes a null element, which must not read as "no invalid entry found".
        var result = new IpAllowlistCondition { Cidrs = [null!] }.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("Invalid CIDR", result.Error);
    }

    [Fact]
    public void Validate_ValidCidrAfterInvalid_ReportsTheInvalidOne()
    {
        var result = new IpAllowlistCondition { Cidrs = ["not-a-cidr", "10.0.0.0/8"] }.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("not-a-cidr", result.Error);
    }

    [Fact]
    public void Validate_ValidCidrs_IsValid()
    {
        var result = new IpAllowlistCondition { Cidrs = ["10.0.0.0/8", "2001:db8::/32"] }.Validate();

        Assert.True(result.IsValid);
    }
}
