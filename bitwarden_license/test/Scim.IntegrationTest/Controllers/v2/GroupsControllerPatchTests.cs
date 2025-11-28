using System.Text.Json;
using Bit.Scim.IntegrationTest.Factories;
using Bit.Scim.Models;
using Bit.Scim.Utilities;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Scim.IntegrationTest.Controllers.v2;

public class GroupsControllerPatchTests : IClassFixture<ScimApplicationFactory>, IAsyncLifetime
{
    private readonly ScimApplicationFactory _factory;

    public GroupsControllerPatchTests(ScimApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        var databaseContext = _factory.GetDatabaseContext();
        _factory.ReinitializeDbForTests(databaseContext);

        return Task.CompletedTask;
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Patch_ReplaceDisplayName_Success()
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        var groupId = ScimApplicationFactory.TestGroupId1;
        var newDisplayName = "Patch Display Name";
        var inputModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>()
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "replace",
                    Value = JsonDocument.Parse($"{{\"displayName\":\"{newDisplayName}\"}}").RootElement
                }
            },
            Schemas = new List<string>() { ScimConstants.Scim2SchemaGroup }
        };

        var context = await _factory.GroupsPatchAsync(organizationId, groupId, inputModel);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);

        var databaseContext = _factory.GetDatabaseContext();
        var group = databaseContext.Groups.FirstOrDefault(g => g.Id == groupId);
        Assert.Equal(newDisplayName, group.Name);

        Assert.Equal(ScimApplicationFactory.InitialGroupUsersCount, databaseContext.GroupUsers.Count());
        Assert.True(databaseContext.GroupUsers.Any(gu => gu.OrganizationUserId == ScimApplicationFactory.TestOrganizationUserId1));
        Assert.True(databaseContext.GroupUsers.Any(gu => gu.OrganizationUserId == ScimApplicationFactory.TestOrganizationUserId4));
    }

    [Fact]
    public async Task Patch_ReplaceMembers_Success()
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        var groupId = ScimApplicationFactory.TestGroupId1;
        var inputModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>()
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "replace",
                    Path = "members",
                    Value = JsonDocument.Parse($"[{{\"value\":\"{ScimApplicationFactory.TestOrganizationUserId2}\"}}]").RootElement
                }
            },
            Schemas = new List<string>() { ScimConstants.Scim2SchemaGroup }
        };

        var context = await _factory.GroupsPatchAsync(organizationId, groupId, inputModel);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);

        var databaseContext = _factory.GetDatabaseContext();
        Assert.Single(databaseContext.GroupUsers);

        Assert.Equal(ScimApplicationFactory.InitialGroupUsersCount - 1, databaseContext.GroupUsers.Count());
        var groupUser = databaseContext.GroupUsers.FirstOrDefault();
        Assert.Equal(ScimApplicationFactory.TestOrganizationUserId2, groupUser.OrganizationUserId);
    }

    [Fact]
    public async Task Patch_AddSingleMember_Success()
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        var groupId = ScimApplicationFactory.TestGroupId1;
        var inputModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>()
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "add",
                    Path = $"members[value eq \"{ScimApplicationFactory.TestOrganizationUserId2}\"]",
                    Value = JsonDocument.Parse("{}").RootElement
                }
            },
            Schemas = new List<string>() { ScimConstants.Scim2SchemaGroup }
        };

        var context = await _factory.GroupsPatchAsync(organizationId, groupId, inputModel);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);

        var databaseContext = _factory.GetDatabaseContext();
        Assert.Equal(ScimApplicationFactory.InitialGroupUsersCount + 1, databaseContext.GroupUsers.Count());
        Assert.True(databaseContext.GroupUsers.Any(gu => gu.GroupId == groupId && gu.OrganizationUserId == ScimApplicationFactory.TestOrganizationUserId1));
        Assert.True(databaseContext.GroupUsers.Any(gu => gu.GroupId == groupId && gu.OrganizationUserId == ScimApplicationFactory.TestOrganizationUserId2));
        Assert.True(databaseContext.GroupUsers.Any(gu => gu.GroupId == groupId && gu.OrganizationUserId == ScimApplicationFactory.TestOrganizationUserId4));
    }

    [Fact]
    public async Task Patch_AddListMembers_Success()
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        var groupId = ScimApplicationFactory.TestGroupId2;
        var inputModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>()
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "add",
                    Path = "members",
                    Value = JsonDocument.Parse($"[{{\"value\":\"{ScimApplicationFactory.TestOrganizationUserId2}\"}},{{\"value\":\"{ScimApplicationFactory.TestOrganizationUserId3}\"}}]").RootElement
                }
            },
            Schemas = new List<string>() { ScimConstants.Scim2SchemaGroup }
        };

        var context = await _factory.GroupsPatchAsync(organizationId, groupId, inputModel);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);

        var databaseContext = _factory.GetDatabaseContext();
        Assert.True(databaseContext.GroupUsers.Any(gu => gu.GroupId == groupId && gu.OrganizationUserId == ScimApplicationFactory.TestOrganizationUserId2));
        Assert.True(databaseContext.GroupUsers.Any(gu => gu.GroupId == groupId && gu.OrganizationUserId == ScimApplicationFactory.TestOrganizationUserId3));
    }

    [Fact]
    public async Task Patch_RemoveSingleMember_ReplaceDisplayName_Success()
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        var groupId = ScimApplicationFactory.TestGroupId1;
        var newDisplayName = "Patch Display Name";
        var inputModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>()
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "remove",
                    Path = $"members[value eq \"{ScimApplicationFactory.TestOrganizationUserId1}\"]",
                    Value = JsonDocument.Parse("{}").RootElement
                },
                new ScimPatchModel.OperationModel
                {
                    Op = "replace",
                    Value = JsonDocument.Parse($"{{\"displayName\":\"{newDisplayName}\"}}").RootElement
                }
            },
            Schemas = new List<string>() { ScimConstants.Scim2SchemaGroup }
        };

        var context = await _factory.GroupsPatchAsync(organizationId, groupId, inputModel);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);

        var databaseContext = _factory.GetDatabaseContext();
        Assert.Equal(ScimApplicationFactory.InitialGroupUsersCount - 1, databaseContext.GroupUsers.Count());
        Assert.Equal(ScimApplicationFactory.InitialGroupCount, databaseContext.Groups.Count());

        var group = databaseContext.Groups.FirstOrDefault(g => g.Id == groupId);
        Assert.Equal(newDisplayName, group.Name);
    }

    [Fact]
    public async Task Patch_RemoveListMembers_Success()
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        var groupId = ScimApplicationFactory.TestGroupId1;
        var inputModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>()
            {
                new ScimPatchModel.OperationModel
                {
                    Op = "remove",
                    Path = "members",
                    Value = JsonDocument.Parse($"[{{\"value\":\"{ScimApplicationFactory.TestOrganizationUserId1}\"}}, {{\"value\":\"{ScimApplicationFactory.TestOrganizationUserId4}\"}}]").RootElement
                }
            },
            Schemas = new List<string>() { ScimConstants.Scim2SchemaGroup }
        };

        var context = await _factory.GroupsPatchAsync(organizationId, groupId, inputModel);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);

        var databaseContext = _factory.GetDatabaseContext();
        Assert.Empty(databaseContext.GroupUsers);
    }

    [Fact]
    public async Task Patch_NotFound()
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        var groupId = Guid.NewGuid();
        var inputModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>(),
            Schemas = new List<string>() { ScimConstants.Scim2SchemaGroup }
        };
        var expectedResponse = new ScimErrorResponseModel
        {
            Status = StatusCodes.Status404NotFound,
            Detail = "Group not found.",
            Schemas = new List<string> { ScimConstants.Scim2SchemaError }
        };

        var context = await _factory.GroupsPatchAsync(organizationId, groupId, inputModel);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

        var responseModel = JsonSerializer.Deserialize<ScimErrorResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);
    }
}
