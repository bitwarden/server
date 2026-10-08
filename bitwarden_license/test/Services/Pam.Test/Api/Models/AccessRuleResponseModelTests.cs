using System.Text.Json;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Models;

public class AccessRuleResponseModelTests
{
    [Fact]
    public void Constructor_ReturnsTheStoredConditionsAsJson()
    {
        const string conditions = """[{"kind":"human_approval","approverCount":1}]""";

        var model = new AccessRuleResponseModel(Details(conditions));

        Assert.Equal(JsonValueKind.Array, model.Conditions!.Value.ValueKind);
        Assert.Equal("human_approval", model.Conditions.Value[0].GetProperty("kind").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_WithoutStoredConditions_ReturnsNullConditions(string? conditions)
    {
        var model = new AccessRuleResponseModel(Details(conditions!));

        Assert.Null(model.Conditions);
    }

    /// <summary>
    /// Rules stored in an earlier conditions format have to stay readable. Evaluation does not read this model; the
    /// resolver fails safe to human approval on the same document.
    /// </summary>
    [Fact]
    public void Constructor_WithStoredConditionsThatDoNotParse_ReturnsNullConditions()
    {
        var model = new AccessRuleResponseModel(Details("{ not json"));

        Assert.Null(model.Conditions);
    }

    /// <summary>
    /// Dapper materializes these as <see cref="DateTimeKind.Unspecified"/>, which JavaScript reads as local time.
    /// </summary>
    [Fact]
    public void Constructor_MarksTheTimestampsAsUtcWithoutShiftingThem()
    {
        var stored = new DateTime(2026, 6, 15, 13, 0, 0, DateTimeKind.Unspecified);
        var details = Details("[]");
        details.CreationDate = stored;
        details.RevisionDate = stored;

        var model = new AccessRuleResponseModel(details);

        Assert.Equal(DateTimeKind.Utc, model.CreationDate.Kind);
        Assert.Equal(DateTimeKind.Utc, model.RevisionDate.Kind);
        Assert.Equal(stored.TimeOfDay, model.CreationDate.TimeOfDay);
        Assert.Equal(stored.TimeOfDay, model.RevisionDate.TimeOfDay);
    }

    [Fact]
    public void Constructor_ReturnsTheGovernedCollections()
    {
        var collectionId = Guid.NewGuid();
        var details = Details("[]");
        details.CollectionIds = [collectionId];

        var model = new AccessRuleResponseModel(details);

        Assert.Equal(new[] { collectionId }, model.Collections.ToArray());
    }

    private static AccessRuleDetails Details(string conditions) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        Name = "Production database",
        Conditions = conditions,
    };
}
