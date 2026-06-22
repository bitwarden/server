using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Commercial.Pam.Api.Models.Request;
using Bit.Pam.Enums;
using Xunit;

namespace Bit.Commercial.Pam.Test.Api.Models;

public class AccessDecisionRequestModelTests
{
    // Mirror the API's web JSON defaults (camelCase, case-insensitive) so the test exercises the real bind path.
    private static readonly JsonSerializerOptions _web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(0, AccessDecisionVerdict.Deny)]
    [InlineData(1, AccessDecisionVerdict.Approve)]
    public void Deserialize_BindsIntegerVerdictToEnum(int wire, AccessDecisionVerdict expected)
    {
        var model = JsonSerializer.Deserialize<AccessDecisionRequestModel>($$"""{"verdict":{{wire}}}""", _web);

        Assert.NotNull(model);
        Assert.Equal(expected, model!.Verdict);
        Assert.Equal(expected, model.ToSubmission().Verdict);
    }

    [Theory]
    [InlineData("\"approve\"")]
    [InlineData("\"1\"")]
    public void Deserialize_StringVerdict_Throws(string wire)
    {
        // The wire format is the integer enum value; a string is no longer accepted.
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<AccessDecisionRequestModel>($$"""{"verdict":{{wire}}}""", _web));
    }

    [Fact]
    public void Validate_MissingVerdict_IsInvalid()
    {
        Assert.Contains(
            Validate(new AccessDecisionRequestModel { Verdict = null }),
            r => r.MemberNames.Contains(nameof(AccessDecisionRequestModel.Verdict)));
    }

    [Fact]
    public void Validate_UndefinedVerdict_IsInvalid()
    {
        Assert.Contains(
            Validate(new AccessDecisionRequestModel { Verdict = (AccessDecisionVerdict)5 }),
            r => r.MemberNames.Contains(nameof(AccessDecisionRequestModel.Verdict)));
    }

    [Theory]
    [InlineData(AccessDecisionVerdict.Approve)]
    [InlineData(AccessDecisionVerdict.Deny)]
    public void Validate_DefinedVerdict_IsValid(AccessDecisionVerdict verdict)
    {
        Assert.Empty(Validate(new AccessDecisionRequestModel { Verdict = verdict }));
    }

    private static List<ValidationResult> Validate(AccessDecisionRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
