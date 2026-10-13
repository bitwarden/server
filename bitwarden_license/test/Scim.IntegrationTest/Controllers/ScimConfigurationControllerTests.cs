using System.Net;
using System.Text.Json;
using Bit.Scim.IntegrationTest.Factories;
using Bit.Scim.Utilities;
using Xunit;

namespace Bit.Scim.IntegrationTest.Controllers;

public class ScimConfigurationControllerTests : IClassFixture<ScimApplicationFactory>
{
    private readonly ScimApplicationFactory _factory;
    private readonly HttpClient _client;

    public ScimConfigurationControllerTests(ScimApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetServiceProviderConfig_Success()
    {
        var response = await _client.GetAsync("/v2/serviceproviderconfig");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/scim+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Contains(ScimConstants.Scim2SchemaServiceProviderConfig,
            json.GetProperty("schemas").EnumerateArray().Select(s => s.GetString()));
        Assert.True(json.GetProperty("patch").GetProperty("supported").GetBoolean());
        Assert.False(json.GetProperty("bulk").GetProperty("supported").GetBoolean());
        Assert.True(json.GetProperty("filter").GetProperty("supported").GetBoolean());
        Assert.False(json.GetProperty("changePassword").GetProperty("supported").GetBoolean());
        Assert.False(json.GetProperty("sort").GetProperty("supported").GetBoolean());
        Assert.False(json.GetProperty("etag").GetProperty("supported").GetBoolean());
        Assert.Equal("ServiceProviderConfig", json.GetProperty("meta").GetProperty("resourceType").GetString());

        var authSchemes = json.GetProperty("authenticationSchemes").EnumerateArray().ToList();
        Assert.Single(authSchemes);
        Assert.Equal("oauthbearertoken", authSchemes[0].GetProperty("type").GetString());
    }

    [Fact]
    public async Task GetSchemas_Success()
    {
        var response = await _client.GetAsync("/v2/schemas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/scim+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Contains(ScimConstants.Scim2SchemaListResponse,
            json.GetProperty("schemas").EnumerateArray().Select(s => s.GetString()));
        Assert.Equal(2, json.GetProperty("totalResults").GetInt32());

        var resources = json.GetProperty("resources").EnumerateArray().ToList();
        Assert.Equal(2, resources.Count);

        var schemaIds = resources.Select(r => r.GetProperty("id").GetString()).ToList();
        Assert.Contains(ScimConstants.Scim2SchemaUser, schemaIds);
        Assert.Contains(ScimConstants.Scim2SchemaGroup, schemaIds);
    }

    [Fact]
    public async Task GetSchemas_ById_User_Success()
    {
        var response = await _client.GetAsync($"/v2/schemas/{Uri.EscapeDataString(ScimConstants.Scim2SchemaUser)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/scim+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(ScimConstants.Scim2SchemaUser, json.GetProperty("id").GetString());
        Assert.Equal("User", json.GetProperty("name").GetString());

        var attributeNames = json.GetProperty("attributes").EnumerateArray()
            .Select(a => a.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("userName", attributeNames);
        Assert.Contains("emails", attributeNames);
        Assert.Contains("active", attributeNames);
        Assert.Contains("externalId", attributeNames);
        Assert.Contains("name", attributeNames);
        Assert.Contains("displayName", attributeNames);
        Assert.Contains("id", attributeNames);
    }

    [Fact]
    public async Task GetSchemas_ById_Group_Success()
    {
        var response = await _client.GetAsync($"/v2/schemas/{Uri.EscapeDataString(ScimConstants.Scim2SchemaGroup)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/scim+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(ScimConstants.Scim2SchemaGroup, json.GetProperty("id").GetString());
        Assert.Equal("Group", json.GetProperty("name").GetString());

        var attributeNames = json.GetProperty("attributes").EnumerateArray()
            .Select(a => a.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("displayName", attributeNames);
        Assert.Contains("members", attributeNames);
        Assert.Contains("externalId", attributeNames);
        Assert.Contains("id", attributeNames);
    }

    [Fact]
    public async Task GetSchemas_ById_NotFound()
    {
        var response = await _client.GetAsync("/v2/schemas/urn:unknown:schema");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/scim+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Contains(ScimConstants.Scim2SchemaError,
            json.GetProperty("schemas").EnumerateArray().Select(s => s.GetString()));
        Assert.Equal(404, json.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task GetResourceTypes_Success()
    {
        var response = await _client.GetAsync("/v2/resourcetypes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/scim+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Contains(ScimConstants.Scim2SchemaListResponse,
            json.GetProperty("schemas").EnumerateArray().Select(s => s.GetString()));
        Assert.Equal(2, json.GetProperty("totalResults").GetInt32());

        var resources = json.GetProperty("resources").EnumerateArray().ToList();
        Assert.Equal(2, resources.Count);

        var userResource = resources.First(r => r.GetProperty("id").GetString() == "User");
        Assert.Equal("User", userResource.GetProperty("name").GetString());
        Assert.Equal("/Users", userResource.GetProperty("endpoint").GetString());
        Assert.Equal(ScimConstants.Scim2SchemaUser, userResource.GetProperty("schema").GetString());

        var groupResource = resources.First(r => r.GetProperty("id").GetString() == "Group");
        Assert.Equal("Group", groupResource.GetProperty("name").GetString());
        Assert.Equal("/Groups", groupResource.GetProperty("endpoint").GetString());
        Assert.Equal(ScimConstants.Scim2SchemaGroup, groupResource.GetProperty("schema").GetString());
    }
}
