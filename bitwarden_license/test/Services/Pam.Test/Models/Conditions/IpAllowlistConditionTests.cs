using System.Net;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Models.Conditions;
using Xunit;

namespace Bit.Services.Pam.Test.Models.Conditions;

public class IpAllowlistConditionTests
{
    private static AccessSignals Signals(IPAddress? ip) => new()
    {
        IpAddress = ip,
        Timestamp = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero),
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
        // An allowlist with no entries permits no address.
        var evaluation = new IpAllowlistCondition().Evaluate(Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinIpRange, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_MalformedCidr_DeniesClosed()
    {
        // A present-but-unparseable CIDR matches no address, so a caller with a known IP still fails closed.
        var condition = new IpAllowlistCondition { Cidrs = ["not-a-cidr"] };

        var evaluation = condition.Evaluate(Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinIpRange, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_LaterCidrMatches_Allows()
    {
        // The caller matches the second entry, so evaluation must not stop at the first non-matching CIDR.
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
    public void Validate_ValidCidrs_IsValid()
    {
        var result = new IpAllowlistCondition { Cidrs = ["10.0.0.0/8", "2001:db8::/32"] }.Validate();

        Assert.True(result.IsValid);
    }
}
