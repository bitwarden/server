using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Api.Models.Request;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.IntegrationTest;

/// <summary>
/// The access-rule CRUD contract over the real request pipeline and SQLite, covering what the Pam.Test unit tests
/// mock away. Every test acts as the owner; authorization is <see cref="AccessRuleAuthorizationTests"/>'s subject.
/// </summary>
public class AccessRuleCrudTests(ApiApplicationFactory factory)
    : AccessRuleIntegrationTestBase(factory, "pam-access-rule-crud")
{
    private const string HumanApproval = """[{"kind":"human_approval","approverCount":1}]""";

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await LoginHelper.LoginAsync(OwnerEmail);
    }

    [Fact]
    public async Task AccessRule_CreateListGetUpdateDelete_RoundTripsOverTheApi()
    {
        var created = await PostRuleAsync(NewRule("Production database"));
        var id = created["id"]!.GetValue<Guid>();
        Assert.Equal("accessRule", created["object"]!.GetValue<string>());
        Assert.Equal(Organization.Id, created["organizationId"]!.GetValue<Guid>());

        var list = await GetJsonAsync(AccessRulesUrl);
        Assert.Equal("list", list["object"]!.GetValue<string>());
        Assert.Contains(list["data"]!.AsArray(), rule => rule!["id"]!.GetValue<Guid>() == id);

        var fetched = await GetJsonAsync(AccessRuleUrl(id));
        Assert.Equal("Production database", fetched["name"]!.GetValue<string>());
        Assert.True(fetched["enabled"]!.GetValue<bool>());

        var updated = await PutRuleAsync(id, NewRule("Production database (paused)", enabled: false));
        Assert.Equal(id, updated["id"]!.GetValue<Guid>());
        Assert.Equal("Production database (paused)", updated["name"]!.GetValue<string>());
        Assert.False(updated["enabled"]!.GetValue<bool>());

        var deleteResponse = await Client.DeleteAsync(AccessRuleUrl(id));
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var afterDelete = await Client.GetAsync(AccessRuleUrl(id));
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    /// <summary>Properties this version does not model survive too, so the engine reads what the client sent.</summary>
    [Fact]
    public async Task Post_StoresTheConditionsDocumentVerbatim()
    {
        const string conditions =
            """[{"kind":"ip_allowlist","cidrs":["10.0.0.0/8","192.168.1.0/24"],"unmodelled":"kept"}]""";

        var created = await PostRuleAsync(NewRule("Office network only", conditions));

        Assert.Equal(JsonNode.Parse(conditions)!.ToJsonString(), created["conditions"]!.ToJsonString());

        var fetched = await GetJsonAsync(AccessRuleUrl(created["id"]!.GetValue<Guid>()));
        Assert.Equal(JsonNode.Parse(conditions)!.ToJsonString(), fetched["conditions"]!.ToJsonString());
    }

    [Fact]
    public async Task Post_GovernsTheRequestedCollections_AndPutReplacesTheSet()
    {
        var governed = await OrganizationTestHelpers.CreateCollectionAsync(Factory, Organization.Id, "Governed");
        var other = await OrganizationTestHelpers.CreateCollectionAsync(Factory, Organization.Id, "Other");

        var created = await PostRuleAsync(NewRule("Governs one collection", collections: [governed.Id]));
        var id = created["id"]!.GetValue<Guid>();

        Assert.Equal(new[] { governed.Id }, created["collections"]!.AsArray().Select(c => c!.GetValue<Guid>()).ToArray());
        Assert.Equal(id, await AccessRuleIdOfAsync(governed.Id));

        await PutRuleAsync(id, NewRule("Governs the other collection", collections: [other.Id]));

        Assert.Null(await AccessRuleIdOfAsync(governed.Id));
        Assert.Equal(id, await AccessRuleIdOfAsync(other.Id));
    }

    /// <summary>The exception filter turns a validator failure into an <c>ErrorResponseModel</c> 400.</summary>
    [Fact]
    public async Task Post_WithACidrThatDoesNotParse_ReturnsBadRequestWithTheValidatorMessage()
    {
        var response = await Client.PostAsJsonAsync(AccessRulesUrl,
            NewRule("Bad allowlist", """[{"kind":"ip_allowlist","cidrs":["not-a-cidr"]}]"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("error", body["object"]!.GetValue<string>());
        Assert.Contains("not-a-cidr", body["message"]!.GetValue<string>());
    }

    /// <summary>Rejected by the validation filter before the handler can dereference the null.</summary>
    [Fact]
    public async Task Post_WithoutConditions_ReturnsBadRequestFromModelValidation()
    {
        var response = await Client.PostAsJsonAsync(AccessRulesUrl,
            new { name = "No conditions", collections = Array.Empty<Guid>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("The model state is invalid.", body["message"]!.GetValue<string>());
        Assert.Contains(nameof(AccessRuleRequestModel.Conditions),
            body["validationErrors"]!.AsObject().Select(error => error.Key));
    }

    /// <summary>
    /// Authorization only proves membership of the route's organization, so the handler has to refuse a rule from
    /// another one.
    /// </summary>
    [Fact]
    public async Task Get_ARuleBelongingToAnotherOrganization_ReturnsNotFound()
    {
        var foreignRule = await SeedRuleInAnotherOrganizationAsync();

        var response = await Client.GetAsync(AccessRuleUrl(foreignRule.Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ARuleBelongingToAnotherOrganization_ReturnsNotFound()
    {
        var foreignRule = await SeedRuleInAnotherOrganizationAsync();

        var response = await Client.DeleteAsync(AccessRuleUrl(foreignRule.Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        Assert.NotNull(await Factory.GetService<IAccessRuleRepository>().GetByIdAsync(foreignRule.Id));
    }

    [Fact]
    public async Task AccessRuleEndpoints_WithThePamFeatureFlagOff_AreNotRoutable()
    {
        FeatureService.IsEnabled(FeatureFlagKeys.Pam).Returns(false);

        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync(AccessRulesUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.PostAsJsonAsync(AccessRulesUrl, NewRule("Should not be reachable"))).StatusCode);
    }

    [Fact]
    public async Task Post_RecordsTheCallingUserAsTheLastEditor()
    {
        var created = await PostRuleAsync(NewRule("Attributed to the owner"));

        var owner = await Factory.GetService<IUserRepository>().GetByEmailAsync(OwnerEmail);
        var stored = await Factory.GetService<IAccessRuleRepository>()
            .GetByIdAsync(created["id"]!.GetValue<Guid>());

        Assert.Equal(owner!.Id, stored!.LastEditedBy);
    }

    /// <summary>A kind-less DateTime serializes without a designator, which JavaScript reads as local time.</summary>
    [Fact]
    public async Task Post_ReturnsTimestampsMarkedAsUtc()
    {
        var created = await PostRuleAsync(NewRule("Timestamped"));

        Assert.EndsWith("Z", created["creationDate"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.EndsWith("Z", created["revisionDate"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>Rules stored in an earlier conditions format have to stay readable.</summary>
    [Fact]
    public async Task Get_WithStoredConditionsThatNoLongerParse_ReturnsNullConditions()
    {
        var rule = await Factory.GetService<IAccessRuleRepository>().CreateAsync(new AccessRule
        {
            OrganizationId = Organization.Id,
            Name = "Unparseable conditions",
            Conditions = "{ not json",
        });

        var fetched = await GetJsonAsync(AccessRuleUrl(rule.Id));

        Assert.Null(fetched["conditions"]);
    }

    private static object NewRule(
        string name,
        string conditions = HumanApproval,
        bool enabled = true,
        Guid[]? collections = null) => new
        {
            name,
            enabled,
            conditions = JsonNode.Parse(conditions),
            collections = collections ?? [],
        };

    private async Task<JsonObject> PostRuleAsync(object rule)
    {
        var response = await Client.PostAsJsonAsync(AccessRulesUrl, rule);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }

    private async Task<JsonObject> PutRuleAsync(Guid id, object rule)
    {
        var response = await Client.PutAsJsonAsync(AccessRuleUrl(id), rule);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }

    private async Task<JsonObject> GetJsonAsync(string url)
    {
        var response = await Client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }

    private static async Task<JsonObject> ReadJsonAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonObject>())!;

    private async Task<Guid?> AccessRuleIdOfAsync(Guid collectionId) =>
        (await Factory.GetService<ICollectionRepository>().GetByIdAsync(collectionId))!.AccessRuleId;

    private async Task<AccessRule> SeedRuleInAnotherOrganizationAsync()
    {
        var otherOwnerEmail = $"pam-other-org-{Guid.NewGuid()}@bitwarden.com";
        await Factory.LoginWithNewAccount(otherOwnerEmail);
        var (otherOrganization, _) = await OrganizationTestHelpers.SignUpAsync(Factory,
            plan: PlanType.EnterpriseAnnually, ownerEmail: otherOwnerEmail, passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);

        // Seeded through the repository, so the test never acts as the other organization's owner.
        var rule = await Factory.GetService<IAccessRuleRepository>().CreateAsync(new AccessRule
        {
            OrganizationId = otherOrganization.Id,
            Name = "Another organization's rule",
            Conditions = "[]",
        });

        await LoginHelper.LoginAsync(OwnerEmail);
        return rule;
    }
}
