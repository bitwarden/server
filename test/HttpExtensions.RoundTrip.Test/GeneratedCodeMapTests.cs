using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Validation;
using Xunit;

namespace Bit.HttpExtensions.RoundTrip.Test;

/// <summary>
/// The generated map, which is what a trimmed or ahead-of-time published minimal API resolves against.
/// </summary>
/// <remarks>
/// These go through <see cref="ValidationCodeMap.TryResolveRegistered"/> rather than
/// <see cref="ValidationCodeMap.TryResolve"/>, so a regression that made the generated path fall back to
/// reflection fails here instead of at publish time.
/// </remarks>
public class GeneratedCodeMapTests
{
    [Fact]
    public void AConstrainedProperty_ResolvesFromTheGeneratedMap()
    {
        Assert.True(ValidationCodeMap.TryResolveRegistered(
            typeof(GeneratedModel), "Name", "whatever the framework said", out var wirePath, out var error));

        Assert.Equal("name", wirePath);
        Assert.Equal(ValidationCodes.TooLong, error.Type);
        Assert.Equal(200, (int)error.Parameters![ValidationParameters.Max]!);
    }

    [Fact]
    public void ARenamedProperty_ReportsTheNameTheClientSent()
    {
        ValidationCodeMap.TryResolveRegistered(typeof(GeneratedModel), "RenamedOnTheWire", "m", out var wirePath, out _);

        Assert.Equal("tag", wirePath);
    }

    [Fact]
    public void ANestedCollection_KeepsTheIndexItFailedAt()
    {
        Assert.True(ValidationCodeMap.TryResolveRegistered(
            typeof(GeneratedModel), "Members[2].Email", "m", out var wirePath, out var error));

        Assert.Equal("members[2].email", wirePath);
        Assert.Equal(ValidationCodes.Required, error.Type);
    }

    [Fact]
    public void ATwoEndedLengthConstraint_ReportsOneCodeCarryingBothBounds()
    {
        ValidationCodeMap.TryResolveRegistered(typeof(GeneratedModel), "Bounded", "m", out _, out var error);

        Assert.Equal(ValidationCodes.InvalidLength, error.Type);
        Assert.Equal(5, (int)error.Parameters![ValidationParameters.Min]!);
        Assert.Equal(200, (int)error.Parameters[ValidationParameters.Max]!);
    }

    [Fact]
    public void APropertyThatCanFailTwoWays_ResolvesByAskingTheConstraintHowItWordsItself()
    {
        // The generated map reconstructs the attribute and asks it, rather than holding a copy of its wording.
        var required = new RequiredAttribute().FormatErrorMessage("AccessCode");
        var tooLong = new StringLengthAttribute(25).FormatErrorMessage("AccessCode");

        ValidationCodeMap.TryResolveRegistered(typeof(GeneratedModel), "AccessCode", required, out _, out var missing);
        ValidationCodeMap.TryResolveRegistered(typeof(GeneratedModel), "AccessCode", tooLong, out _, out var overlong);

        Assert.Equal(ValidationCodes.Required, missing.Type);
        Assert.Equal(ValidationCodes.TooLong, overlong.Type);
        Assert.Equal(25, (int)overlong.Parameters![ValidationParameters.Max]!);
    }

    [Fact]
    public void AConstraintThatCannotBeReconstructed_IsIdentifiedByElimination()
    {
        // MaxLength's constructor is [RequiresUnreferencedCode], so generated code cannot ask it for its wording.
        // Required identifies itself, and the remaining failure must be the other one.
        var required = new RequiredAttribute().FormatErrorMessage("Capped");

        ValidationCodeMap.TryResolveRegistered(typeof(GeneratedModel), "Capped", required, out _, out var missing);
        ValidationCodeMap.TryResolveRegistered(typeof(GeneratedModel), "Capped", "anything else", out _, out var other);

        Assert.Equal(ValidationCodes.Required, missing.Type);
        Assert.Equal(ValidationCodes.TooLong, other.Type);
        Assert.Equal(50, (int)other.Parameters![ValidationParameters.Max]!);
    }

    [Fact]
    public void AnExplicitErrorMessage_IsWhatTheConstraintIsAskedToProduce()
    {
        ValidationCodeMap.TryResolveRegistered(
            typeof(GeneratedModel), "Custom", "Custom is required, please.", out _, out var error);

        Assert.Equal(ValidationCodes.Required, error.Type);
    }

    [Fact]
    public void TheFrameworksOwnMarker_AlsoTriggersGeneration()
    {
        // A minimal API being validated by AddValidation() already carries [ValidatableType], so it should not
        // have to carry ours as well. This is also what pins the attribute's metadata name, which moved
        // namespaces in .NET 10 GA.
        Assert.True(ValidationCodeMap.TryResolveRegistered(
            typeof(ValidatableTypeModel), "Reason", "m", out var wirePath, out var error));

        Assert.Equal("reason", wirePath);
        Assert.Equal(ValidationCodes.Required, error.Type);
    }

    [Fact]
    public void AnUnmarkedModel_IsNotInTheGeneratedMap() =>
        // Only marked roots are generated; an unmarked one resolves by reflection instead.
        Assert.False(ValidationCodeMap.TryResolveRegistered(typeof(RoundTripModel), "Reason", "m", out _, out _));
}

[ValidatableType]
public sealed class ValidatableTypeModel
{
    [Required]
    public string? Reason { get; set; }
}

public sealed class GeneratedMember
{
    [Required]
    public string? Email { get; set; }
}

[GenerateValidationCodes]
public sealed class GeneratedModel
{
    [StringLength(200)]
    public string? Name { get; set; }

    [StringLength(200, MinimumLength = 5)]
    public string? Bounded { get; set; }

    [Required]
    [StringLength(25)]
    public string? AccessCode { get; set; }

    [Required]
    [MaxLength(50)]
    public string? Capped { get; set; }

    [Required(ErrorMessage = "Custom is required, please.")]
    public string? Custom { get; set; }

    [JsonPropertyName("tag")]
    [Required]
    public string? RenamedOnTheWire { get; set; }

    public List<GeneratedMember>? Members { get; set; }
}
