using System.Text.Json;
using Bit.Scim.IntegrationTest.Factories;
using Bit.Scim.Models;
using Bit.Scim.Utilities;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Scim.IntegrationTest.Controllers.v2;

public class GroupsControllerTests : IClassFixture<ScimApplicationFactory>, IAsyncLifetime
{
    private readonly ScimApplicationFactory _factory;

    public GroupsControllerTests(ScimApplicationFactory factory)
    {
        _factory = factory;
    }

    public ValueTask InitializeAsync()
    {
        var databaseContext = _factory.GetDatabaseContext();
        _factory.ReinitializeDbForTests(databaseContext);
        return ValueTask.CompletedTask;
    }

    ValueTask IAsyncDisposable.DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Get_Success()
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        var groupId = ScimApplicationFactory.TestGroupId1;
        var expectedResponse = new ScimGroupResponseModel
        {
            Id = groupId,
            DisplayName = "Test Group 1",
            ExternalId = "A",
            Schemas = new List<string> { ScimConstants.Scim2SchemaGroup }
        };

        var context = await _factory.GroupsGetAsync(organizationId, groupId);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var responseModel = JsonSerializer.Deserialize<ScimGroupResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);

        Assert.Contains("application/scim+json", context.Response.Headers.ContentType.ToString());
    }

    [Fact]
    public async Task Get_NotFound()
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        var groupId = Guid.NewGuid();
        var expectedResponse = new ScimErrorResponseModel
        {
            Status = StatusCodes.Status404NotFound,
            Detail = "Group not found.",
            Schemas = new List<string> { ScimConstants.Scim2SchemaError }
        };

        var context = await _factory.GroupsGetAsync(organizationId, groupId);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

        var responseModel = JsonSerializer.Deserialize<ScimErrorResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);
    }

    [Fact]
    public async Task GetList_Success()
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        string filter = null;
        int? itemsPerPage = 2;
        int? startIndex = 1;
        var expectedResponse = new ScimListResponseModel<ScimGroupResponseModel>
        {
            ItemsPerPage = itemsPerPage.Value,
            TotalResults = 3,
            StartIndex = startIndex.Value,
            Resources = new List<ScimGroupResponseModel>
            {
                new ScimGroupResponseModel
                {
                    Id = ScimApplicationFactory.TestGroupId1,
                    DisplayName = "Test Group 1",
                    ExternalId = "A",
                    Schemas = new List<string> { ScimConstants.Scim2SchemaGroup }
                },
                new ScimGroupResponseModel
                {
                    Id = ScimApplicationFactory.TestGroupId2,
                    DisplayName = "Test Group 2",
                    ExternalId = "B",
                    Schemas = new List<string> { ScimConstants.Scim2SchemaGroup }
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaListResponse }
        };

        var context = await _factory.GroupsGetListAsync(organizationId, filter, itemsPerPage, startIndex);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var responseModel = JsonSerializer.Deserialize<ScimListResponseModel<ScimGroupResponseModel>>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);
    }

    [Fact]
    public async Task GetList_SearchDisplayName_Success()
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        string filter = "displayName eq Test Group 2";
        int? itemsPerPage = 10;
        int? startIndex = 1;
        var expectedResponse = new ScimListResponseModel<ScimGroupResponseModel>
        {
            ItemsPerPage = itemsPerPage.Value,
            TotalResults = 1,
            StartIndex = startIndex.Value,
            Resources = new List<ScimGroupResponseModel>
            {
                new ScimGroupResponseModel
                {
                    Id = ScimApplicationFactory.TestGroupId2,
                    DisplayName = "Test Group 2",
                    ExternalId = "B",
                    Schemas = new List<string> { ScimConstants.Scim2SchemaGroup }
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaListResponse }
        };

        var context = await _factory.GroupsGetListAsync(organizationId, filter, itemsPerPage, startIndex);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var responseModel = JsonSerializer.Deserialize<ScimListResponseModel<ScimGroupResponseModel>>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);
    }

    [Fact]
    public async Task GetList_SearchExternalId_Success()
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        string filter = "externalId eq C";
        int? itemsPerPage = 10;
        int? startIndex = 1;
        var expectedResponse = new ScimListResponseModel<ScimGroupResponseModel>
        {
            ItemsPerPage = itemsPerPage.Value,
            TotalResults = 1,
            StartIndex = startIndex.Value,
            Resources = new List<ScimGroupResponseModel>
            {
                new ScimGroupResponseModel
                {
                    Id = ScimApplicationFactory.TestGroupId3,
                    DisplayName = "Test Group 3",
                    ExternalId = "C",
                    Schemas = new List<string> { ScimConstants.Scim2SchemaGroup }
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaListResponse }
        };


        var context = await _factory.GroupsGetListAsync(organizationId, filter, itemsPerPage, startIndex);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var responseModel = JsonSerializer.Deserialize<ScimListResponseModel<ScimGroupResponseModel>>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);
    }

    [Fact]
    public async Task GetList_EmptyResult_Success()
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        string filter = "externalId eq Z";
        int? itemsPerPage = 10;
        int? startIndex = 1;
        var expectedResponse = new ScimListResponseModel<ScimGroupResponseModel>
        {
            ItemsPerPage = itemsPerPage.Value,
            TotalResults = 0,
            StartIndex = startIndex.Value,
            Resources = new List<ScimGroupResponseModel>(),
            Schemas = new List<string> { ScimConstants.Scim2SchemaListResponse }
        };


        var context = await _factory.GroupsGetListAsync(organizationId, filter, itemsPerPage, startIndex);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var responseModel = JsonSerializer.Deserialize<ScimListResponseModel<ScimGroupResponseModel>>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);
    }

    [Fact]
    public async Task GetList_SearchDisplayNameWithoutOptionalParameters_Success()
    {
        string filter = "displayName eq Test Group 2";
        int? itemsPerPage = null;
        int? startIndex = null;
        var expectedResponse = new ScimListResponseModel<ScimGroupResponseModel>
        {
            ItemsPerPage = 50, //default value
            TotalResults = 1,
            StartIndex = 1, //default value
            Resources = new List<ScimGroupResponseModel>
            {
                new ScimGroupResponseModel
                {
                    Id = ScimApplicationFactory.TestGroupId2,
                    DisplayName = "Test Group 2",
                    ExternalId = "B",
                    Schemas = new List<string> { ScimConstants.Scim2SchemaGroup }
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaListResponse }
        };

        var context = await _factory.GroupsGetListAsync(ScimApplicationFactory.TestOrganizationId1, filter, itemsPerPage, startIndex);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var responseModel = JsonSerializer.Deserialize<ScimListResponseModel<ScimGroupResponseModel>>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);
    }

    [Fact]
    public async Task Post_Success()
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        var displayName = "New Group";
        var externalId = Guid.NewGuid().ToString();
        var inputModel = new ScimGroupRequestModel
        {
            DisplayName = displayName,
            ExternalId = externalId.ToString(),
            Members = new List<ScimGroupRequestModel.GroupMembersModel>
            {
                new ScimGroupRequestModel.GroupMembersModel { Display = "user1@example.com", Value = ScimApplicationFactory.TestOrganizationUserId1.ToString() }
            },
            Schemas = new List<string>() { ScimConstants.Scim2SchemaGroup }
        };
        var expectedResponse = new ScimGroupResponseModel
        {
            DisplayName = displayName,
            ExternalId = externalId,
            Schemas = new List<string> { ScimConstants.Scim2SchemaGroup }
        };

        var context = await _factory.GroupsPostAsync(organizationId, inputModel);

        Assert.Equal(StatusCodes.Status201Created, context.Response.StatusCode);

        // Verifying that the response includes a header with the URL of the created Group
        Assert.Contains(context.Response.Headers, h => h.Key == "Location");

        var responseModel = JsonSerializer.Deserialize<ScimGroupResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel, "Id");

        var databaseContext = _factory.GetDatabaseContext();
        Assert.Equal(ScimApplicationFactory.InitialGroupCount + 1, databaseContext.Groups.Count());
        Assert.True(databaseContext.Groups.Any(g => g.Name == displayName && g.ExternalId == externalId));

        Assert.Equal(ScimApplicationFactory.InitialGroupUsersCount + 1, databaseContext.GroupUsers.Count());
        Assert.True(databaseContext.GroupUsers.Any(gu => gu.GroupId == responseModel.Id && gu.OrganizationUserId == ScimApplicationFactory.TestOrganizationUserId1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Post_InvalidDisplayName_BadRequest(string? displayName)
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        var model = new ScimGroupRequestModel
        {
            DisplayName = displayName,
            ExternalId = null,
            Members = null,
            Schemas = new List<string>() { ScimConstants.Scim2SchemaGroup }
        };

        var context = await _factory.GroupsPostAsync(organizationId, model);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task Post_ExistingExternalId_Conflict()
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        var model = new ScimGroupRequestModel
        {
            DisplayName = "New Group",
            ExternalId = "A",
            Members = null,
            Schemas = new List<string>() { ScimConstants.Scim2SchemaGroup }
        };

        var context = await _factory.GroupsPostAsync(organizationId, model);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);

        var databaseContext = _factory.GetDatabaseContext();
        Assert.Equal(ScimApplicationFactory.InitialGroupCount, databaseContext.Groups.Count());
        Assert.False(databaseContext.Groups.Any(g => g.Name == "New Group"));
    }

    [Fact]
    public async Task Put_ChangeNameAndMembers_Success()
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        var groupId = ScimApplicationFactory.TestGroupId1;
        var newGroupName = Guid.NewGuid().ToString();
        var inputModel = new ScimGroupRequestModel
        {
            DisplayName = newGroupName,
            ExternalId = "A",
            Members = new List<ScimGroupRequestModel.GroupMembersModel>
            {
                new ScimGroupRequestModel.GroupMembersModel { Display = "user2@example.com", Value = ScimApplicationFactory.TestOrganizationUserId2.ToString() },
                new ScimGroupRequestModel.GroupMembersModel { Display = "user3@example.com", Value = ScimApplicationFactory.TestOrganizationUserId3.ToString() }
            },
            Schemas = new List<string>() { ScimConstants.Scim2SchemaGroup }
        };
        var expectedResponse = new ScimGroupResponseModel
        {
            Id = groupId,
            DisplayName = newGroupName,
            ExternalId = "A",
            Schemas = new List<string>() { ScimConstants.Scim2SchemaGroup }
        };

        var context = await _factory.GroupsPutAsync(organizationId, groupId, inputModel);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var responseModel = JsonSerializer.Deserialize<ScimGroupResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);

        var databaseContext = _factory.GetDatabaseContext();
        var firstGroup = databaseContext.Groups.FirstOrDefault(g => g.Id == groupId);
        Assert.Equal(newGroupName, firstGroup.Name);

        Assert.Equal(2, databaseContext.GroupUsers.Count(gu => gu.GroupId == groupId));
        Assert.NotNull(databaseContext.GroupUsers.FirstOrDefault(gu => gu.GroupId == groupId && gu.OrganizationUserId == ScimApplicationFactory.TestOrganizationUserId2));
        Assert.NotNull(databaseContext.GroupUsers.FirstOrDefault(gu => gu.GroupId == groupId && gu.OrganizationUserId == ScimApplicationFactory.TestOrganizationUserId3));
    }

    [Fact]
    public async Task Put_NotFound()
    {
        var newGroupName = "Test Group 1 New Name";
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        var groupId = Guid.NewGuid();
        var inputModel = new ScimGroupRequestModel
        {
            DisplayName = newGroupName,
            ExternalId = "A",
            Members = new List<ScimGroupRequestModel.GroupMembersModel>(),
            Schemas = new List<string>() { ScimConstants.Scim2SchemaGroup }
        };
        var expectedResponse = new ScimErrorResponseModel
        {
            Status = StatusCodes.Status404NotFound,
            Detail = "Group not found.",
            Schemas = new List<string> { ScimConstants.Scim2SchemaError }
        };

        var context = await _factory.GroupsPutAsync(organizationId, groupId, inputModel);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

        var responseModel = JsonSerializer.Deserialize<ScimErrorResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);
    }

    [Fact]
    public async Task Delete_Success()
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        var groupId = ScimApplicationFactory.TestGroupId3;

        var context = await _factory.GroupsDeleteAsync(organizationId, groupId);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);

        var databaseContext = _factory.GetDatabaseContext();
        Assert.Equal(ScimApplicationFactory.InitialGroupCount - 1, databaseContext.Groups.Count());
        Assert.True(databaseContext.Groups.FirstOrDefault(g => g.Id == groupId) == null);
    }

    [Fact]
    public async Task Delete_NotFound()
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        var groupId = Guid.NewGuid();
        var expectedResponse = new ScimErrorResponseModel
        {
            Status = StatusCodes.Status404NotFound,
            Detail = "Group not found.",
            Schemas = new List<string> { ScimConstants.Scim2SchemaError }
        };

        var context = await _factory.GroupsDeleteAsync(organizationId, groupId);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

        var responseModel = JsonSerializer.Deserialize<ScimErrorResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);
    }
}
