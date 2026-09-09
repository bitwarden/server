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
    /// A rule stored before a conditions-format change has to stay readable, so an unparseable document reads back as
    /// no conditions rather than failing the request. Note the engine treats a rule with no conditions as satisfied,
    /// which makes this a fail-open read — deliberate, and worth knowing about when the format next changes.
    /// </summary>
    [Fact]
    public void Constructor_WithStoredConditionsThatDoNotParse_ReturnsNullConditions()
    {
        var model = new AccessRuleResponseModel(Details("{ not json"));

        Assert.Null(model.Conditions);
    }

    /// <summary>
    /// Dapper materializes these timestamps with <see cref="DateTimeKind.Unspecified"/>, which serializes without a
    /// timezone designator and is then read as local time by a JavaScript client. The stored values are already UTC
    /// instants, so the kind is relabelled — the clock must not move.
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
