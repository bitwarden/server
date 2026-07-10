using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Services.Pam.Api.Models.Request;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Models;

/// <summary>
/// Tests for the <see cref="AccessConditionModel"/> hierarchy: serialization round-trips,
/// polymorphic deserialization, and DataAnnotations validation (including the nested-condition
/// validation path in <see cref="AccessRuleRequestModel"/>).
/// </summary>
public class AccessConditionModelTests
{
    // Use the same options the PAM minimal APIs bind with: Web defaults (camelCase) plus
    // AllowOutOfOrderMetadataProperties, configured in AddPamServices so the 'kind' discriminator
    // is accepted at any position, matching the SDK's serde tagging.
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
    // Deserialization tests
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
    // unmapped member and the request fails at binding.
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
    // Validation tests
    // -------------------------------------------------------------------------

    [Fact]
    public void EmptyConditionsArray_PassesValidation()
    {
        var model = ValidRequestModel(conditions: []);

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results,
            validateAllProperties: true);

        Assert.True(isValid, string.Join(", ", results.Select(r => r.ErrorMessage)));
    }

    [Fact]
    public void MissingConditions_FailsValidation()
    {
        // A request that omits the conditions field entirely must fail [Required] — an omitted
        // field and an explicitly empty array are distinct contract states.
        var model = ValidRequestModel(conditions: null!);

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(AccessRuleRequestModel.Conditions)));
    }

    [Fact]
    public void MissingKindCondition_FailsValidation()
    {
        // A payload with no 'kind' binds to the concrete base type (see
        // Deserialize_MissingKind_BindsToBaseType); validation must reject it.
        var model = ValidRequestModel(conditions: [new AccessConditionModel()]);

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains("Conditions[0]"));
    }

    [Fact]
    public void NullConditionElement_FailsValidation()
    {
        var model = ValidRequestModel(conditions: [null!]);

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains("Conditions[0]"));
    }

    [Fact]
    public void NullCidrEntry_NestedInRequest_FailsValidation()
    {
        var model = ValidRequestModel(conditions:
        [
            new IpAllowlistConditionModel { Cidrs = ["10.0.0.0/8", null!] }
        ]);

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r =>
            r.MemberNames.Any(m => m.StartsWith("Conditions[0]")));
    }

    [Fact]
    public void InvalidCidrs_ReportIndexedMembers_WithoutEchoingValues()
    {
        var model = ValidRequestModel(conditions:
        [
            new IpAllowlistConditionModel { Cidrs = ["10.0.0.0/8", "bad-cidr/99", "10.0.0.1/8"] }
        ]);

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results,
            validateAllProperties: true);

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
        var model = ValidRequestModel(conditions:
        [
            new IpAllowlistConditionModel { Cidrs = [overlong] }
        ]);

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains("Conditions[0].Cidrs[0]"));
    }

    [Fact]
    public void ExactlyHundredCidrs_PassesValidation()
    {
        var cidrs = Enumerable.Range(1, 100).Select(i => $"10.{i}.0.0/16").ToList();
        var model = ValidRequestModel(conditions: [new IpAllowlistConditionModel { Cidrs = cidrs }]);

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results,
            validateAllProperties: true);

        Assert.True(isValid, string.Join(", ", results.Select(r => r.ErrorMessage)));
    }

    [Fact]
    public void MoreThanHundredCidrs_FailsValidation()
    {
        var cidrs = Enumerable.Range(1, 101).Select(i => $"10.{i}.0.0/16").ToList();
        var model = ValidRequestModel(conditions: [new IpAllowlistConditionModel { Cidrs = cidrs }]);

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r =>
            r.MemberNames.Any(m => m.StartsWith("Conditions[0]")));
    }

    [Fact]
    public void MaxLength_MoreThan10Conditions_FailsValidation()
    {
        var conditions = Enumerable.Range(0, 11)
            .Select(_ => (AccessConditionModel)new HumanApprovalConditionModel())
            .ToList();
        var model = ValidRequestModel(conditions: conditions);

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(AccessRuleRequestModel.Conditions)));
    }

    [Fact]
    public void ExactlyTen_Conditions_PassesValidation()
    {
        var conditions = Enumerable.Range(0, 10)
            .Select(_ => (AccessConditionModel)new HumanApprovalConditionModel())
            .ToList();
        var model = ValidRequestModel(conditions: conditions);

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results,
            validateAllProperties: true);

        Assert.True(isValid, string.Join(", ", results.Select(r => r.ErrorMessage)));
    }

    [Fact]
    public void EmptyCidrs_NestedInRequest_FailsValidation()
    {
        var model = ValidRequestModel(conditions:
        [
            new IpAllowlistConditionModel { Cidrs = [] }
        ]);

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results,
            validateAllProperties: true);

        Assert.False(isValid);
        // The member name should be prefixed with the index (Conditions[0].Cidrs)
        Assert.Contains(results, r =>
            r.MemberNames.Any(m => m.StartsWith("Conditions[0]")));
    }

    [Fact]
    public void InvalidCidr_NestedInRequest_FailsValidation()
    {
        var model = ValidRequestModel(conditions:
        [
            new IpAllowlistConditionModel { Cidrs = ["10.0.0.1/8"] } // host bits set
        ]);

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r =>
            r.MemberNames.Any(m => m.StartsWith("Conditions[0]")));
    }

    [Fact]
    public void ValidIpAllowlist_PassesValidation()
    {
        var model = ValidRequestModel(conditions:
        [
            new IpAllowlistConditionModel { Cidrs = ["10.0.0.0/8", "2001:db8::/32"] }
        ]);

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results,
            validateAllProperties: true);

        Assert.True(isValid, string.Join(", ", results.Select(r => r.ErrorMessage)));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static AccessRuleRequestModel ValidRequestModel(List<AccessConditionModel> conditions) =>
        new()
        {
            Name = "Test rule",
            Conditions = conditions,
            Collections = [Guid.NewGuid()],
        };
}
