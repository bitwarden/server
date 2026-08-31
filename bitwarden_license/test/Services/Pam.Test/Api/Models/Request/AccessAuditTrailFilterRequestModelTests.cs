using System.ComponentModel.DataAnnotations;
using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Services.Pam.OrganizationFeatures.Queries;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Models.Request;

/// <summary>
/// The audit trail's query parameters, and what they refuse. Two of the refusals matter more than the rest: an
/// unknown event kind and a token this endpoint did not issue both have a tempting "just ignore it" reading that
/// would answer the caller with the wrong trail rather than an error.
/// </summary>
public class AccessAuditTrailFilterRequestModelTests
{
    [Fact]
    public void ToQueryOptions_WithNothingSet_SelectsNothingAndBoundsNothing()
    {
        var options = new AccessAuditTrailFilterRequestModel().ToQueryOptions();

        Assert.Null(options.Start);
        Assert.Null(options.End);
        Assert.Empty(options.Kinds);
        Assert.Empty(options.ActorIds);
        Assert.False(options.IncludeAutomatedActor);
        Assert.Empty(options.RequesterIds);
        Assert.Empty(options.CipherIds);
        Assert.Empty(options.RuleIds);
        Assert.Null(options.BeforeOccurredAt);
        Assert.Null(options.BeforeId);
    }

    // The chips are multi-select, so a dimension carries a list and the values within it are OR-ed.
    [Fact]
    public void ToQueryOptions_ReadsEachKindOffTheGovernanceVocabulary()
    {
        var model = new AccessAuditTrailFilterRequestModel { Kind = ["requestApproved", "leaseRevoked"] };

        var options = model.ToQueryOptions();

        Assert.Equal(
            [AccessAuditEventKind.RequestApproved, AccessAuditEventKind.LeaseRevoked],
            options.Kinds);
        Assert.Empty(Validate(model));
    }

    // Named rather than ignored: a filter the server did not understand would otherwise be reported as a trail with
    // nothing in it, which on an audit surface reads as "this never happened".
    [Theory]
    [InlineData("requestapproved")]
    [InlineData("RequestApproved")]
    [InlineData("somethingElse")]
    [InlineData("")]
    public void Validate_UnknownKind_IsRejected(string kind)
    {
        var model = new AccessAuditTrailFilterRequestModel { Kind = [kind] };

        var error = Assert.Single(Validate(model));

        Assert.Contains(nameof(AccessAuditTrailFilterRequestModel.Kind), error.MemberNames);
        Assert.Throws<BadRequestException>(() => model.ToQueryOptions());
    }

    // One Item selection spanning both columns: an auditor picking a credential and a rule is asking for either.
    [Fact]
    public void ToQueryOptions_CarriesBothHalvesOfAnItemSelection()
    {
        var cipherId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();

        var options = new AccessAuditTrailFilterRequestModel
        {
            CipherId = [cipherId],
            RuleId = [ruleId],
        }.ToQueryOptions();

        Assert.Equal([cipherId], options.CipherIds);
        Assert.Equal([ruleId], options.RuleIds);
    }

    [Fact]
    public void ToQueryOptions_ReadsAContinuationTokenBackIntoAPosition()
    {
        var occurredAt = new DateTime(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc);
        var id = Guid.NewGuid();
        var model = new AccessAuditTrailFilterRequestModel
        {
            ContinuationToken = $"{occurredAt.Ticks}_{id:N}",
        };

        var options = model.ToQueryOptions();

        Assert.Equal(occurredAt, options.BeforeOccurredAt);
        Assert.Equal(id, options.BeforeId);
        Assert.Empty(Validate(model));
    }

    // A caller walking every page -- the CSV export does -- must not be handed the first page when it asked for the
    // fifth: that loops forever, or writes a file of repeats.
    [Theory]
    [InlineData("not-a-token")]
    [InlineData("638000000000000000")]
    [InlineData("_0123456789abcdef0123456789abcdef")]
    [InlineData("638000000000000000_not-a-guid")]
    [InlineData("-1_0123456789abcdef0123456789abcdef")]
    public void Validate_AContinuationTokenThisEndpointDidNotIssue_IsRejected(string token)
    {
        var model = new AccessAuditTrailFilterRequestModel { ContinuationToken = token };

        var error = Assert.Single(Validate(model));

        Assert.Contains(nameof(AccessAuditTrailFilterRequestModel.ContinuationToken), error.MemberNames);
        Assert.Throws<BadRequestException>(() => model.ToQueryOptions());
    }

    // A bound spelled with an explicit offset deserializes as Local and would otherwise shift the window by the
    // host's UTC offset; one spelled with no designator at all is already the instant the caller meant, and is only
    // relabelled. Both must land on the same stored instant whatever the host's timezone.
    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    public void ToQueryOptions_NormalisesTheBoundsOntoUtc(DateTimeKind kind)
    {
        var wall = new DateTime(2026, 7, 3, 12, 0, 0);
        var start = DateTime.SpecifyKind(wall, kind);
        var expected = kind == DateTimeKind.Local
            ? start.ToUniversalTime()
            : DateTime.SpecifyKind(wall, DateTimeKind.Utc);

        var options = new AccessAuditTrailFilterRequestModel { Start = start, End = start.AddDays(1) }
            .ToQueryOptions();

        Assert.Equal(DateTimeKind.Utc, options.Start!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, options.End!.Value.Kind);
        Assert.Equal(expected, options.Start!.Value);
        Assert.Equal(expected.AddDays(1), options.End!.Value);
    }

    private static List<ValidationResult> Validate(AccessAuditTrailFilterRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
