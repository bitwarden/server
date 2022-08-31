using System.Text.Json;
using Bit.Scim.IntegrationTest.Factories;
using Bit.Scim.Models;
using Bit.Scim.Utilities;
using Xunit;

namespace Bit.Scim.IntegrationTest.Controllers.v2
{
    public class GroupsControllerTests : IClassFixture<ScimApplicationFactory>, IAsyncLifetime
    {
        private readonly ScimApplicationFactory _factory;

        public GroupsControllerTests(ScimApplicationFactory factory)
        {
            _factory = factory;
            _factory.DatabaseName = "test_database_groups";
        }

        public Task InitializeAsync()
        {
            var databaseContext = _factory.GetDatabaseContext();
            _factory.ReinitializeDbForTests(databaseContext);
            return Task.CompletedTask;
        }

        Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task Get_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = ScimApplicationFactory.TestGroupId1;

            var context = await _factory.GroupsGetAsync(organizationId, id);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            var responseModel = JsonSerializer.Deserialize<ScimGroupResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.Equal(ScimApplicationFactory.TestGroupId1, responseModel.Id);
            Assert.Equal("Test Group 1", responseModel.DisplayName);
            Assert.Equal("A", responseModel.ExternalId);
            Assert.Equal(new List<string> { ScimConstants.Scim2SchemaGroup }, responseModel.Schemas);
        }

        [Fact]
        public async Task Get_NotFound()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = Guid.NewGuid();
            var context = await _factory.GroupsGetAsync(organizationId, id.ToString());

            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

            var responseModel = JsonSerializer.Deserialize<ScimErrorResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.Equal(404, responseModel.Status);
            Assert.Equal("Group not found.", responseModel.Detail);
            Assert.Equal(new List<string> { ScimConstants.Scim2SchemaError }, responseModel.Schemas);
        }

        [Fact]
        public async Task GetList_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var context = await _factory.GroupsGetListAsync(organizationId, null, 2, 1);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            var responseModel = JsonSerializer.Deserialize<ScimListResponseModel<ScimGroupResponseModel>>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.Equal(2, responseModel.ItemsPerPage);
            Assert.Equal(3, responseModel.TotalResults);
            Assert.Equal(1, responseModel.StartIndex);

            Assert.Equal(2, responseModel.Resources.Count);
            Assert.Equal(new List<string> { ScimConstants.Scim2SchemaListResponse }, responseModel.Schemas);
        }

        [Fact]
        public async Task GetList_SearchDisplayName_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var context = await _factory.GroupsGetListAsync(organizationId, "displayName eq Test Group 2", 10, 1);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            var responseModel = JsonSerializer.Deserialize<ScimListResponseModel<ScimGroupResponseModel>>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.Single(responseModel.Resources);
            Assert.Equal(10, responseModel.ItemsPerPage);
            Assert.Equal(1, responseModel.TotalResults);
            Assert.Equal(1, responseModel.StartIndex);

            var group = responseModel.Resources.Single();
            Assert.Equal(ScimApplicationFactory.TestGroupId2, group.Id);
            Assert.Equal("Test Group 2", group.DisplayName);
            Assert.Equal("B", group.ExternalId);
            Assert.Equal(new List<string> { ScimConstants.Scim2SchemaListResponse }, responseModel.Schemas);
        }

        [Fact]
        public async Task GetList_SearchExternalId_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var context = await _factory.GroupsGetListAsync(organizationId, "externalId eq C", 10, 1);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            var responseModel = JsonSerializer.Deserialize<ScimListResponseModel<ScimGroupResponseModel>>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.Single(responseModel.Resources);
            Assert.Equal(10, responseModel.ItemsPerPage);
            Assert.Equal(1, responseModel.TotalResults);
            Assert.Equal(1, responseModel.StartIndex);

            var group = responseModel.Resources.Single();
            Assert.Equal(ScimApplicationFactory.TestGroupId3.ToString(), group.Id);
            Assert.Equal("Test Group 3", group.DisplayName);
            Assert.Equal("C", group.ExternalId);
            Assert.Equal(new List<string> { ScimConstants.Scim2SchemaListResponse }, responseModel.Schemas);
        }

        [Fact]
        public async Task Post_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var model = new ScimGroupRequestModel
            {
                DisplayName = "New Group",
                ExternalId = null,
                Members = null,
                Schemas = null
            };

            var context = await _factory.GroupsPostAsync(organizationId, model);

            Assert.Equal(StatusCodes.Status201Created, context.Response.StatusCode);

            var databaseContext = _factory.GetDatabaseContext();
            Assert.Equal(4, databaseContext.Groups.Count());
            Assert.True(databaseContext.Groups.Any(g => g.Name == "New Group"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task Post_InvalidDisplayName_BadRequest(string displayName)
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var model = new ScimGroupRequestModel
            {
                DisplayName = displayName,
                ExternalId = null,
                Members = null,
                Schemas = null
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
                Schemas = null
            };

            var context = await _factory.GroupsPostAsync(organizationId, model);

            Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);

            var databaseContext = _factory.GetDatabaseContext();
            Assert.Equal(3, databaseContext.Groups.Count());
            Assert.False(databaseContext.Groups.Any(g => g.Name == "New Group"));
        }

        [Fact]
        public async Task Put_ChangeName_Success()
        {
            var newGroupName = "Test Group 1 New Name";
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = ScimApplicationFactory.TestGroupId1;
            var model = new ScimGroupRequestModel
            {
                DisplayName = newGroupName,
                ExternalId = "AA",
                Members = new List<ScimGroupRequestModel.GroupMembersModel>(),
                Schemas = new List<string>()
            };

            var context = await _factory.GroupsPutAsync(organizationId, id, model);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            var databaseContext = _factory.GetDatabaseContext();
            Assert.Equal(3, databaseContext.Groups.Count());

            var firstGroup = databaseContext.Groups.FirstOrDefault(g => g.Id.ToString() == id);
            Assert.Equal(newGroupName, firstGroup.Name);
        }

        [Fact]
        public async Task Put_NotFound()
        {
            var newGroupName = "Test Group 1 New Name";
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = Guid.NewGuid();
            var model = new ScimGroupRequestModel
            {
                DisplayName = newGroupName,
                ExternalId = "AA",
                Members = new List<ScimGroupRequestModel.GroupMembersModel>(),
                Schemas = new List<string>()
            };

            var context = await _factory.GroupsPutAsync(organizationId, id.ToString(), model);

            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

            var responseModel = JsonSerializer.Deserialize<ScimErrorResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.Equal(404, responseModel.Status);
            Assert.Equal("Group not found.", responseModel.Detail);
            Assert.Equal(new List<string> { ScimConstants.Scim2SchemaError }, responseModel.Schemas);

            var databaseContext = _factory.GetDatabaseContext();
            Assert.Equal(3, databaseContext.Groups.Count());
            Assert.True(databaseContext.Groups.FirstOrDefault(g => g.Id == id) == null);
        }

        [Fact]
        public async Task Patch_ReplaceDisplayName_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = ScimApplicationFactory.TestGroupId1;
            var model = new ScimPatchModel
            {
                Operations = new List<ScimPatchModel.OperationModel>()
                {
                    new ScimPatchModel.OperationModel
                    {
                        Op = "replace",
                        Value = JsonDocument.Parse("{\"displayName\":\"Patch Display Name\"}").RootElement
                    }
                },
                Schemas = new List<string>()
            };

            var context = await _factory.GroupsPatchAsync(organizationId, id, model);

            Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);

            var databaseContext = _factory.GetDatabaseContext();
            Assert.Equal(3, databaseContext.Groups.Count());

            var group = databaseContext.Groups.FirstOrDefault(g => g.Id.ToString() == id);
            Assert.Equal("Patch Display Name", group.Name);
        }

        [Fact]
        public async Task Patch_ReplaceMembers_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = ScimApplicationFactory.TestGroupId1;
            var model = new ScimPatchModel
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
                Schemas = new List<string>()
            };

            var context = await _factory.GroupsPatchAsync(organizationId, id, model);

            Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);

            var databaseContext = _factory.GetDatabaseContext();
            Assert.Single(databaseContext.GroupUsers);

            var groupUser = databaseContext.GroupUsers.FirstOrDefault();
            Assert.Equal(ScimApplicationFactory.TestOrganizationUserId2, groupUser.OrganizationUserId.ToString());
        }

        [Fact]
        public async Task Patch_AddSingleMember_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = ScimApplicationFactory.TestGroupId1;
            var model = new ScimPatchModel
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
                Schemas = new List<string>()
            };

            var context = await _factory.GroupsPatchAsync(organizationId, id, model);

            Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);

            var databaseContext = _factory.GetDatabaseContext();
            Assert.Equal(2, databaseContext.GroupUsers.Count());
        }

        [Fact]
        public async Task Patch_AddListMembers_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = ScimApplicationFactory.TestGroupId2;
            var model = new ScimPatchModel
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
                Schemas = new List<string>()
            };

            var context = await _factory.GroupsPatchAsync(organizationId, id, model);

            Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);

            var databaseContext = _factory.GetDatabaseContext();
            Assert.Equal(3, databaseContext.GroupUsers.Count());
        }

        [Fact]
        public async Task Patch_RemoveSingleMember_ReplaceDisplayName_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = ScimApplicationFactory.TestGroupId1;
            var model = new ScimPatchModel
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
                        Value = JsonDocument.Parse("{\"displayName\":\"Patch Display Name\"}").RootElement
                    }
                },
                Schemas = new List<string>()
            };

            var context = await _factory.GroupsPatchAsync(organizationId, id, model);

            Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);

            var databaseContext = _factory.GetDatabaseContext();
            Assert.Empty(databaseContext.GroupUsers);
            Assert.Equal(3, databaseContext.Groups.Count());

            var group = databaseContext.Groups.FirstOrDefault(g => g.Id.ToString() == id);
            Assert.Equal("Patch Display Name", group.Name);
        }

        [Fact]
        public async Task Patch_RemoveListMembers_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = ScimApplicationFactory.TestGroupId1;
            var model = new ScimPatchModel
            {
                Operations = new List<ScimPatchModel.OperationModel>()
                {
                    new ScimPatchModel.OperationModel
                    {
                        Op = "remove",
                        Path = "members",
                        Value = JsonDocument.Parse($"[{{\"value\":\"{ScimApplicationFactory.TestOrganizationUserId1}\"}}]").RootElement
                    }
                },
                Schemas = new List<string>()
            };

            var context = await _factory.GroupsPatchAsync(organizationId, id, model);

            Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);

            var databaseContext = _factory.GetDatabaseContext();
            Assert.Empty(databaseContext.GroupUsers);
        }

        [Fact]
        public async Task Patch_NotFound()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = Guid.NewGuid();
            var model = new Models.ScimPatchModel
            {
                Operations = new List<ScimPatchModel.OperationModel>(),
                Schemas = new List<string>()
            };

            var context = await _factory.GroupsPatchAsync(organizationId, id.ToString(), model);

            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

            var responseModel = JsonSerializer.Deserialize<ScimErrorResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.Equal(404, responseModel.Status);
            Assert.Equal("Group not found.", responseModel.Detail);
            Assert.Equal(new List<string> { ScimConstants.Scim2SchemaError }, responseModel.Schemas);

            var databaseContext = _factory.GetDatabaseContext();
            Assert.Equal(3, databaseContext.Groups.Count());
            Assert.True(databaseContext.Groups.FirstOrDefault(g => g.Id == id) == null);
        }

        [Fact]
        public async Task Delete_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = ScimApplicationFactory.TestGroupId3;

            var context = await _factory.GroupsDeleteAsync(organizationId, id);

            Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);

            var databaseContext = _factory.GetDatabaseContext();
            Assert.Equal(2, databaseContext.Groups.Count());
            Assert.True(databaseContext.Groups.FirstOrDefault(g => g.Id.ToString() == id) == null);
        }

        [Fact]
        public async Task Delete_NotFound()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = Guid.NewGuid();

            var context = await _factory.GroupsDeleteAsync(organizationId, id.ToString());

            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

            var responseModel = JsonSerializer.Deserialize<ScimErrorResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.Equal(404, responseModel.Status);
            Assert.Equal("Group not found.", responseModel.Detail);
            Assert.Equal(new List<string> { ScimConstants.Scim2SchemaError }, responseModel.Schemas);

            var databaseContext = _factory.GetDatabaseContext();
            Assert.Equal(3, databaseContext.Groups.Count());
            Assert.True(databaseContext.Groups.FirstOrDefault(g => g.Id == id) == null);
        }
    }
}
