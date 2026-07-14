using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Services.Pam.Api.Models.Request;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Models;

/// <summary>
/// Tests for the <see cref="AccessConditionModel"/> hierarchy: serialization round-trips,
/// polymorphic deserialization, and the fail-closed "double decode" validation path in
/// <see cref="AccessRuleRequestModel"/>, where verbatim-JSON conditions are decoded into the typed
/// union and validated server-side.
/// </summary>
public class AccessConditionModelTests
{
    // The same options AccessRuleRequestModel decodes conditions with: Web defaults (camelCase)
    // plus AllowOutOfOrderMetadataProperties so the 'kind' discriminator is accepted at any
    // position, matching the SDK's serde tagging.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        AllowOutOfOrderMetadataProperties = true,
    };

    // -------------------------------------------------------------------------
    // Serialization tests
    // -------------------------------------------------------------------------

    [Fact]
    public void HumanApproval_SerializesTo_BareKindOnly()
    {
        AccessConditionModel condition = new HumanApprovalConditionModel();

        var json = JsonSerializer.Serialize(condition, JsonOptions);

        Assert.Equal("""{"kind":"human_approval"}""", json);
    }

    [Fact]
    public void IpAllowlist_SerializesTo_KindAndCidrs()
    {
        AccessConditionModel condition = new IpAllowlistConditionModel
        {
            Cidrs = ["10.0.0.0/8"]
        };

        var json = JsonSerializer.Serialize(condition, JsonOptions);

        Assert.Equal("""{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]}""", json);
    }

    // -------------------------------------------------------------------------
    // Deserialization tests (the typed union, decoded in isolation)
    // -------------------------------------------------------------------------

    [Fact]
    public void Deserialize_UnknownKind_ThrowsJsonException()
    {
        // An unrecognized discriminator value is rejected fail-closed.
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<AccessConditionModel>(
                """{"kind":"unknown_thing"}""", JsonOptions));
    }

    [Fact]
    public void Deserialize_KindNotFirstProperty_Succeeds()
    {
        // serde's internally-tagged enums accept the tag at any position, so the server must too.
        var condition = JsonSerializer.Deserialize<AccessConditionModel>(
            """{"cidrs":["10.0.0.0/8"],"kind":"ip_allowlist"}""", JsonOptions);

        var ipAllowlist = Assert.IsType<IpAllowlistConditionModel>(condition);
        var cidr = Assert.Single(ipAllowlist.Cidrs);
        Assert.Equal("10.0.0.0/8", cidr);
    }

    [Fact]
    public void Deserialize_MissingKind_BindsToBaseType()
    {
        // Binding falls back to the concrete base instead of throwing; the base instance is then
        // rejected by AccessRuleRequestModel.Validate (see MissingKindCondition_FailsValidation).
        var condition = JsonSerializer.Deserialize<AccessConditionModel>("{}", JsonOptions);

        Assert.NotNull(condition);
        Assert.Equal(typeof(AccessConditionModel), condition.GetType());
    }

    [Theory]
    // No 'kind' means the payload binds as the (member-less) base type, so every property is an
    // unmapped member and the payload fails to decode.
    [InlineData("""{"cidrs":["10.0.0.0/8"]}""")]
    // The discriminator matches case-sensitively, mirroring the SDK's serde tag, so a PascalCase
    // "Kind" is an unknown member, not a discriminator.
    [InlineData("""{"Kind":"human_approval"}""")]
    public void Deserialize_MissingKindWithMembers_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<AccessConditionModel>(json, JsonOptions));
    }

    [Theory]
    [InlineData("""{"kind":null}""")]
    [InlineData("""{"kind":42}""")]
    public void Deserialize_NonStringKind_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<AccessConditionModel>(json, JsonOptions));
    }

    [Fact]
    public void Deserialize_NonObjectCondition_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<AccessConditionModel>(
                "\"human_approval\"", JsonOptions));
    }

    [Theory]
    // Unknown members are rejected fail-closed ([JsonUnmappedMemberHandling(Disallow)]): silently
    // dropping an unrecognized constraint would enforce the condition more loosely than intended.
    [InlineData("""{"kind":"human_approval","extra":"rejected"}""")]
    [InlineData("""{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"],"expires":"2026-08-01"}""")]
    public void Deserialize_UnknownMember_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<AccessConditionModel>(json, JsonOptions));
    }

    // -------------------------------------------------------------------------
    // Request validation: the "double decode" path
    // -------------------------------------------------------------------------

    [Fact]
    public void EmptyConditionsArray_PassesValidation()
    {
        var (isValid, results) = Validate(RequestWith());

        Assert.True(isValid, string.Join(", ", results.Select(r => r.ErrorMessage)));
    }

    [Fact]
    public void MissingConditions_FailsValidation()
    {
        // A request that omits the conditions field entirely must fail [Required] — an omitted
        // field and an explicitly empty array are distinct contract states.
        var (isValid, results) = Validate(RequestWithoutConditions());

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(AccessRuleRequestModel.Conditions)));
    }

    [Fact]
    public void NonArrayConditions_FailsValidation()
    {
        var (isValid, results) = Validate(RequestWithRawConditions("""{"kind":"human_approval"}"""));

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(AccessRuleRequestModel.Conditions)));
    }

    [Fact]
    public void MissingKindCondition_FailsValidation()
    {
        // A payload with no 'kind' binds to the concrete base type; validation must reject it.
        var (isValid, results) = Validate(RequestWithRawConditions("[{}]"));

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains("Conditions[0]"));
    }

    [Fact]
    public void UnknownKindCondition_FailsValidation()
    {
        // The core fail-closed guarantee: a kind the server does not model is rejected, never
        // silently accepted/dropped.
        var (isValid, results) = Validate(RequestWithRawConditions("""[{"kind":"time_of_day"}]"""));

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains("Conditions[0]"));
    }

    [Fact]
    public void UnknownMemberCondition_FailsValidation()
    {
        var (isValid, results) = Validate(
            RequestWithRawConditions("""[{"kind":"human_approval","extra":"rejected"}]"""));

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains("Conditions[0]"));
    }

    [Fact]
    public void KindNotFirstProperty_PassesValidation()
    {
        var (isValid, results) = Validate(
            RequestWithRawConditions("""[{"cidrs":["10.0.0.0/8"],"kind":"ip_allowlist"}]"""));

        Assert.True(isValid, string.Join(", ", results.Select(r => r.ErrorMessage)));
    }

    [Fact]
    public void NullConditionElement_FailsValidation()
    {
        var (isValid, results) = Validate(RequestWithRawConditions("[null]"));

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains("Conditions[0]"));
    }

    [Fact]
    public void NullCidrEntry_NestedInRequest_FailsValidation()
    {
        var (isValid, results) = Validate(RequestWith(
            new IpAllowlistConditionModel { Cidrs = ["10.0.0.0/8", null!] }));

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Any(m => m.StartsWith("Conditions[0]")));
    }

    [Fact]
    public void InvalidCidrs_ReportIndexedMembers_WithoutEchoingValues()
    {
        var (isValid, results) = Validate(RequestWith(
            new IpAllowlistConditionModel { Cidrs = ["10.0.0.0/8", "bad-cidr/99", "10.0.0.1/8"] }));

        Assert.False(isValid);
        // Each failing entry gets its own indexed member key so clients can tell them apart...
        Assert.Contains(results, r => r.MemberNames.Contains("Conditions[0].Cidrs[1]"));
        Assert.Contains(results, r => r.MemberNames.Contains("Conditions[0].Cidrs[2]"));
        Assert.DoesNotContain(results, r => r.MemberNames.Contains("Conditions[0].Cidrs[0]"));
        // ...and the raw user-supplied value never appears in the message.
        Assert.DoesNotContain(results, r => r.ErrorMessage!.Contains("bad-cidr"));
    }

    [Fact]
    public void OverlongCidrEntry_FailsValidation()
    {
        // A degenerate zero-padded prefix parses as a valid CIDR but must be blocked by the
        // per-entry length cap before it can be persisted.
        var overlong = "10.0.0.0/" + new string('0', 300) + "8";
        var (isValid, results) = Validate(RequestWith(
            new IpAllowlistConditionModel { Cidrs = [overlong] }));

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains("Conditions[0].Cidrs[0]"));
    }

    [Fact]
    public void ExactlyHundredCidrs_PassesValidation()
    {
        var cidrs = Enumerable.Range(1, 100).Select(i => $"10.{i}.0.0/16").ToList();
        var (isValid, results) = Validate(RequestWith(new IpAllowlistConditionModel { Cidrs = cidrs }));

        Assert.True(isValid, string.Join(", ", results.Select(r => r.ErrorMessage)));
    }

    [Fact]
    public void MoreThanHundredCidrs_FailsValidation()
    {
        var cidrs = Enumerable.Range(1, 101).Select(i => $"10.{i}.0.0/16").ToList();
        var (isValid, results) = Validate(RequestWith(new IpAllowlistConditionModel { Cidrs = cidrs }));

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Any(m => m.StartsWith("Conditions[0]")));
    }

    [Fact]
    public void MoreThanTenConditions_FailsValidation()
    {
        var conditions = Enumerable.Range(0, 11)
            .Select(_ => (AccessConditionModel)new HumanApprovalConditionModel())
            .ToArray();
        var (isValid, results) = Validate(RequestWith(conditions));

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(AccessRuleRequestModel.Conditions)));
    }

    [Fact]
    public void ExactlyTenConditions_PassesValidation()
    {
        var conditions = Enumerable.Range(0, 10)
            .Select(_ => (AccessConditionModel)new HumanApprovalConditionModel())
            .ToArray();
        var (isValid, results) = Validate(RequestWith(conditions));

        Assert.True(isValid, string.Join(", ", results.Select(r => r.ErrorMessage)));
    }

    [Fact]
    public void EmptyCidrs_NestedInRequest_FailsValidation()
    {
        var (isValid, results) = Validate(RequestWith(new IpAllowlistConditionModel { Cidrs = [] }));

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Any(m => m.StartsWith("Conditions[0]")));
    }

    [Fact]
    public void InvalidCidr_NestedInRequest_FailsValidation()
    {
        var (isValid, results) = Validate(RequestWith(
            new IpAllowlistConditionModel { Cidrs = ["10.0.0.1/8"] })); // host bits set

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Any(m => m.StartsWith("Conditions[0]")));
    }

    [Fact]
    public void ValidConditions_PassValidation()
    {
        var (isValid, results) = Validate(RequestWith(
            new HumanApprovalConditionModel(),
            new IpAllowlistConditionModel { Cidrs = ["10.0.0.0/8", "2001:db8::/32"] }));

        Assert.True(isValid, string.Join(", ", results.Select(r => r.ErrorMessage)));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static (bool IsValid, List<ValidationResult> Results) Validate(AccessRuleRequestModel model)
    {
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(
            model, new ValidationContext(model), results, validateAllProperties: true);
        return (isValid, results);
    }

    // Builds a request whose Conditions is the given typed conditions serialized to verbatim JSON,
    // exercising the same serialize -> store -> decode path the wire contract uses.
    private static AccessRuleRequestModel RequestWith(params AccessConditionModel[] conditions) =>
        RequestWithRawConditions(JsonSerializer.Serialize(conditions, JsonOptions));

    // Builds a request from a raw conditions JSON string, for shapes that can't be produced by
    // serializing typed models (missing kind, null element, unknown members, non-array, ...).
    private static AccessRuleRequestModel RequestWithRawConditions(string conditionsJson) =>
        new()
        {
            Name = "Test rule",
            Conditions = JsonSerializer.Deserialize<JsonElement>(conditionsJson),
            Collections = [Guid.NewGuid()],
        };

    private static AccessRuleRequestModel RequestWithoutConditions() =>
        new()
        {
            Name = "Test rule",
            Conditions = null,
            Collections = [Guid.NewGuid()],
        };
}
