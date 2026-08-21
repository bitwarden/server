using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Xunit;

namespace Bit.HttpExtensions.Test;

public class ValidationCodeMapTests
{
    private sealed class Member
    {
        [Required]
        public string? Email { get; set; }
    }

    private sealed class Address
    {
        [Required]
        public string? Line { get; set; }
    }

    private sealed class Owner
    {
        public List<Address>? Addresses { get; set; }
    }

    private sealed class Root
    {
        [StringLength(200)]
        public string? Name { get; set; }

        [Required]
        [StringLength(25)]
        public string? AccessCode { get; set; }

        [StringLength(200, MinimumLength = 5)]
        public string? Bounded { get; set; }

        [Range(1, 100)]
        public int Seats { get; set; }

        [JsonPropertyName("tag")]
        [Required]
        public string? RenamedOnTheWire { get; set; }

        public string? Unconstrained { get; set; }

        public List<Member>? Members { get; set; }

        public Owner? Owner { get; set; }
    }

    private sealed class OtherRoot
    {
        [Required]
        public string? Name { get; set; }
    }

    [Fact]
    public void AConstrainedProperty_ResolvesToItsCodeAndWireName()
    {
        Assert.True(ValidationCodeMap.TryResolve(typeof(Root), "Name", "too long", out var wirePath, out var error));

        Assert.Equal("name", wirePath);
        Assert.Equal(ValidationCodes.TooLong, error.Type);
        Assert.Equal("too long", error.Detail);
        Assert.Equal(200, (int)error.Parameters![ValidationParameters.Max]!);
    }

    [Fact]
    public void ATwoEndedConstraint_ReportsOneCodeCarryingBothBounds()
    {
        ValidationCodeMap.TryResolve(typeof(Root), "Bounded", "m", out _, out var error);

        Assert.Equal(ValidationCodes.InvalidLength, error.Type);
        Assert.Equal(5, (int)error.Parameters![ValidationParameters.Min]!);
        Assert.Equal(200, (int)error.Parameters[ValidationParameters.Max]!);
    }

    [Fact]
    public void ARange_CarriesBothBounds()
    {
        ValidationCodeMap.TryResolve(typeof(Root), "Seats", "m", out _, out var error);

        Assert.Equal(ValidationCodes.OutOfRange, error.Type);
        Assert.Equal(1, (int)error.Parameters![ValidationParameters.Min]!);
        Assert.Equal(100, (int)error.Parameters[ValidationParameters.Max]!);
    }

    [Fact]
    public void ARenamedProperty_ReportsTheNameTheClientSent()
    {
        ValidationCodeMap.TryResolve(typeof(Root), "RenamedOnTheWire", "m", out var wirePath, out _);

        Assert.Equal("tag", wirePath);
    }

    [Fact]
    public void TheSamePathUnderTwoRoots_ResolvesSeparately()
    {
        // Keying on the path alone would let one request model silently take the other's code.
        ValidationCodeMap.TryResolve(typeof(Root), "Name", "m", out _, out var fromRoot);
        ValidationCodeMap.TryResolve(typeof(OtherRoot), "Name", "m", out _, out var fromOther);

        Assert.Equal(ValidationCodes.TooLong, fromRoot.Type);
        Assert.Equal(ValidationCodes.Required, fromOther.Type);
    }

    [Fact]
    public void ACollectionElement_KeepsTheIndexItFailedAt()
    {
        Assert.True(ValidationCodeMap.TryResolve(
            typeof(Root), "Members[3].Email", "required", out var wirePath, out var error));

        Assert.Equal("members[3].email", wirePath);
        Assert.Equal(ValidationCodes.Required, error.Type);
    }

    [Fact]
    public void ACollectionNestedUnderAProperty_RestoresItsIndex()
    {
        Assert.True(ValidationCodeMap.TryResolve(
            typeof(Root), "Owner.Addresses[2].Line", "required", out var wirePath, out _));

        Assert.Equal("owner.addresses[2].line", wirePath);
    }

    [Fact]
    public void APropertyThatCanFailTwoWays_ResolvesByTheMessageTheFrameworkRecorded()
    {
        // Asking the framework for the message rather than holding a copy of it is what makes this survive a
        // reword: both sides move together.
        var required = new RequiredAttribute().FormatErrorMessage("AccessCode");
        var tooLong = new StringLengthAttribute(25).FormatErrorMessage("AccessCode");

        ValidationCodeMap.TryResolve(typeof(Root), "AccessCode", required, out _, out var missing);
        ValidationCodeMap.TryResolve(typeof(Root), "AccessCode", tooLong, out _, out var overlong);

        Assert.Equal(ValidationCodes.Required, missing.Type);
        Assert.Equal(ValidationCodes.TooLong, overlong.Type);
        Assert.Equal(25, (int)overlong.Parameters![ValidationParameters.Max]!);
    }

    [Fact]
    public void AnAmbiguousPathWithAMessageNoCandidateClaims_ResolvesToNothing() =>
        // Reporting one of the two at random would be worse than reporting it uncoded.
        Assert.False(ValidationCodeMap.TryResolve(typeof(Root), "AccessCode", "reworded upstream", out _, out _));

    [Fact]
    public void AnUnambiguousPath_IgnoresTheMessageEntirely()
    {
        // The single-constraint case is what keeps most of this independent of framework wording.
        Assert.True(ValidationCodeMap.TryResolve(typeof(Root), "Name", "anything at all", out _, out var error));

        Assert.Equal(ValidationCodes.TooLong, error.Type);
    }

    [Fact]
    public void AnUnconstrainedProperty_ResolvesToNothing() =>
        Assert.False(ValidationCodeMap.TryResolve(typeof(Root), "Unconstrained", "m", out _, out _));

    [Fact]
    public void APathNamingNoProperty_ResolvesToNothing() =>
        Assert.False(ValidationCodeMap.TryResolve(typeof(Root), "NotAProperty", "m", out _, out _));

    [Fact]
    public void APathThroughNothing_ResolvesToNothing() =>
        Assert.False(ValidationCodeMap.TryResolve(typeof(Root), "Name.Deeper", "m", out _, out _));

    [Fact]
    public void ParametersAreNotSharedBetweenResolutions()
    {
        ValidationCodeMap.TryResolve(typeof(Root), "Name", "m", out _, out var first);
        ValidationCodeMap.TryResolve(typeof(Root), "Name", "m", out _, out var second);

        Assert.NotSame(first.Parameters, second.Parameters);
    }

    [Fact]
    public void NullRootType_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ValidationCodeMap.TryResolve(null!, "Name", "m", out _, out _));
}
