using System.Text.Json;
using Bit.Core.Enums;
using Bit.IntegrationTestCommon.Factories;
using Bit.Scim.Models;
using Bit.Scim.Utilities;
using Xunit;

namespace Bit.Scim.IntegrationTest.Controllers.v2
{
    public class UsersControllerTests : IClassFixture<ScimApplicationFactory>
    {
        private readonly ScimApplicationFactory _factory;

        public UsersControllerTests(ScimApplicationFactory factory)
        {
            _factory = factory;
            _factory.DatabaseName = "test_database_users";

            var databaseContext = factory.GetDatabaseContext();
            _factory.ReinitializeDbForTests(databaseContext);
        }

        [Fact]
        public async Task Get_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = ScimApplicationFactory.TestOrganizationUserId1;

            var context = await _factory.UsersGetAsync(organizationId, id);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            var responseModel = JsonSerializer.Deserialize<ScimUserResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.Equal(ScimApplicationFactory.TestOrganizationUserId1, responseModel.Id);
            Assert.Equal("Test User 1", responseModel.DisplayName);
            Assert.Equal("UA", responseModel.ExternalId);
            Assert.Equal(new List<string> { ScimConstants.Scim2SchemaUser }, responseModel.Schemas);
        }

        [Fact]
        public async Task Get_NotFound()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = Guid.NewGuid();
            var context = await _factory.UsersGetAsync(organizationId, id.ToString());

            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

            var responseModel = JsonSerializer.Deserialize<ScimErrorResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.Equal(404, responseModel.Status);
            Assert.Equal("User not found.", responseModel.Detail);
            Assert.Equal(new List<string> { ScimConstants.Scim2SchemaError }, responseModel.Schemas);
        }

        [Fact]
        public async Task GetList_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var context = await _factory.UsersGetListAsync(organizationId, null, 2, 1);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            var responseModel = JsonSerializer.Deserialize<ScimListResponseModel<ScimUserResponseModel>>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.Equal(2, responseModel.ItemsPerPage);
            Assert.Equal(3, responseModel.TotalResults);
            Assert.Equal(1, responseModel.StartIndex);

            Assert.Equal(2, responseModel.Resources.Count);
            Assert.Equal(new List<string> { ScimConstants.Scim2SchemaListResponse }, responseModel.Schemas);
        }

        [Fact]
        public async Task GetList_SearchEmail_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var context = await _factory.UsersGetListAsync(organizationId, "userName eq user2@mail.com", 10, 1);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            var responseModel = JsonSerializer.Deserialize<ScimListResponseModel<ScimUserResponseModel>>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.Single(responseModel.Resources);
            Assert.Equal(10, responseModel.ItemsPerPage);
            Assert.Equal(1, responseModel.TotalResults);
            Assert.Equal(1, responseModel.StartIndex);

            var user = responseModel.Resources.Single();
            Assert.Equal(ScimApplicationFactory.TestOrganizationUserId2.ToString(), user.Id);
            Assert.Equal("Test User 2", user.DisplayName);
            Assert.Equal("UB", user.ExternalId);
            Assert.Equal(new List<string> { ScimConstants.Scim2SchemaListResponse }, responseModel.Schemas);
        }

        [Fact]
        public async Task GetList_SearchExternalId_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var context = await _factory.UsersGetListAsync(organizationId, "externalId eq UC", 10, 1);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            var responseModel = JsonSerializer.Deserialize<ScimListResponseModel<ScimUserResponseModel>>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.Single(responseModel.Resources);
            Assert.Equal(10, responseModel.ItemsPerPage);
            Assert.Equal(1, responseModel.TotalResults);
            Assert.Equal(1, responseModel.StartIndex);

            var user = responseModel.Resources.Single();
            Assert.Equal(ScimApplicationFactory.TestOrganizationUserId3.ToString(), user.Id);
            Assert.Equal("Test User 3", user.DisplayName);
            Assert.Equal("UC", user.ExternalId);
            Assert.Equal(new List<string> { ScimConstants.Scim2SchemaListResponse }, responseModel.Schemas);
        }

        [Fact]
        public async Task Post_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var model = new ScimUserRequestModel
            {
                DisplayName = "New User",
                Emails = new List<BaseScimUserModel.EmailModel> { new BaseScimUserModel.EmailModel("user4@mail.com") },
                ExternalId = null,
                Schemas = null,
                Active = true
            };

            var context = await _factory.UsersPostAsync(organizationId, model);

            Assert.Equal(StatusCodes.Status201Created, context.Response.StatusCode);

            var databaseContext = _factory.GetDatabaseContext();
            Assert.Equal(4, databaseContext.OrganizationUsers.Count());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task Post_InvalidDisplayName_BadRequest(string displayName)
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var model = new ScimUserRequestModel
            {
                DisplayName = displayName,
                ExternalId = null,
                Schemas = null,
                Active = true
            };

            var context = await _factory.UsersPostAsync(organizationId, model);

            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        }

        [Fact]
        public async Task Post_ExistingEmail_Conflict()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var model = new ScimUserRequestModel
            {
                DisplayName = "New User",
                Emails = new List<BaseScimUserModel.EmailModel> { new BaseScimUserModel.EmailModel("user1@mail.com") },
                ExternalId = "UA",
                Schemas = null,
                Active = true
            };

            var context = await _factory.UsersPostAsync(organizationId, model);

            Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);

            var databaseContext = _factory.GetDatabaseContext();
            Assert.Equal(3, databaseContext.OrganizationUsers.Count());
        }

        [Fact]
        public async Task Put_RevokeUser_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = ScimApplicationFactory.TestOrganizationUserId2;
            var model = new ScimUserRequestModel
            {
                Active = false
            };

            var context = await _factory.UsersPutAsync(organizationId, id, model);

            var responseModel = JsonSerializer.Deserialize<ScimUserResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.False(responseModel.Active);

            var databaseContext = _factory.GetDatabaseContext();
            var revokedUser = databaseContext.OrganizationUsers.FirstOrDefault(g => g.Id.ToString() == id);
            Assert.Equal(OrganizationUserStatusType.Revoked, revokedUser.Status);
        }

        [Fact]
        public async Task Put_ActivateUser_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = ScimApplicationFactory.TestOrganizationUserId3;
            var model = new ScimUserRequestModel
            {
                Active = true
            };

            var context = await _factory.UsersPutAsync(organizationId, id, model);

            var responseModel = JsonSerializer.Deserialize<ScimUserResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.True(responseModel.Active);

            var databaseContext = _factory.GetDatabaseContext();
            var revokedUser = databaseContext.OrganizationUsers.FirstOrDefault(g => g.Id.ToString() == id);
            Assert.NotEqual(OrganizationUserStatusType.Revoked, revokedUser.Status);
        }

        [Fact]
        public async Task Put_NotFound()
        {
            var newGroupName = "Test Group 1 New Name";
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = Guid.NewGuid();
            var model = new ScimUserRequestModel
            {
                DisplayName = newGroupName,
                ExternalId = "AA",
                Schemas = new List<string>()
            };

            var context = await _factory.UsersPutAsync(organizationId, id.ToString(), model);

            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

            var responseModel = JsonSerializer.Deserialize<ScimErrorResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.Equal(404, responseModel.Status);
            Assert.Equal("User not found.", responseModel.Detail);
            Assert.Equal(new List<string> { ScimConstants.Scim2SchemaError }, responseModel.Schemas);

            var databaseContext = _factory.GetDatabaseContext();
            Assert.Equal(3, databaseContext.OrganizationUsers.Count());
            Assert.True(databaseContext.OrganizationUsers.FirstOrDefault(g => g.Id == id) == null);
        }

        [Fact]
        public async Task Patch_ReplaceRevoke_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = ScimApplicationFactory.TestOrganizationUserId2;
            var model = new ScimPatchModel
            {
                Operations = new List<ScimPatchModel.OperationModel>()
                {
                    new ScimPatchModel.OperationModel { Op = "replace", Value = JsonDocument.Parse("{\"active\":false}").RootElement  },
                },
                Schemas = new List<string>()
            };

            var context = await _factory.UsersPatchAsync(organizationId, id, model);

            Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);

            var databaseContext = _factory.GetDatabaseContext();

            var organizationUser = databaseContext.OrganizationUsers.FirstOrDefault(g => g.Id.ToString() == id);
            Assert.Equal(OrganizationUserStatusType.Revoked, organizationUser.Status);
        }

        [Fact]
        public async Task Patch_ReplaceRestore_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = ScimApplicationFactory.TestOrganizationUserId3;
            var model = new ScimPatchModel
            {
                Operations = new List<ScimPatchModel.OperationModel>()
                {
                    new ScimPatchModel.OperationModel { Op = "replace", Value = JsonDocument.Parse("{\"active\":true}").RootElement  },
                },
                Schemas = new List<string>()
            };

            var context = await _factory.UsersPatchAsync(organizationId, id, model);

            Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);

            var databaseContext = _factory.GetDatabaseContext();

            var organizationUser = databaseContext.OrganizationUsers.FirstOrDefault(g => g.Id.ToString() == id);
            Assert.NotEqual(OrganizationUserStatusType.Revoked, organizationUser.Status);
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

            var context = await _factory.UsersPatchAsync(organizationId, id.ToString(), model);

            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

            var responseModel = JsonSerializer.Deserialize<ScimErrorResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.Equal(404, responseModel.Status);
            Assert.Equal("User not found.", responseModel.Detail);
            Assert.Equal(new List<string> { ScimConstants.Scim2SchemaError }, responseModel.Schemas);
        }

        [Fact]
        public async Task Delete_Success()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = ScimApplicationFactory.TestOrganizationUserId1;
            var model = new ScimUserRequestModel();

            var context = await _factory.UsersDeleteAsync(organizationId, id, model);

            Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);

            var databaseContext = _factory.GetDatabaseContext();
            Assert.Equal(2, databaseContext.OrganizationUsers.Count());
            Assert.False(databaseContext.OrganizationUsers.Any(g => g.Id.ToString() == id));
        }

        [Fact]
        public async Task Delete_NotFound()
        {
            var organizationId = ScimApplicationFactory.TestOrganizationId1;
            var id = Guid.NewGuid();
            var model = new ScimUserRequestModel();

            var context = await _factory.UsersDeleteAsync(organizationId, id.ToString(), model);

            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

            var responseModel = JsonSerializer.Deserialize<ScimErrorResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.Equal(404, responseModel.Status);
            Assert.Equal("User not found.", responseModel.Detail);
            Assert.Equal(new List<string> { ScimConstants.Scim2SchemaError }, responseModel.Schemas);

            var databaseContext = _factory.GetDatabaseContext();
            Assert.Equal(3, databaseContext.OrganizationUsers.Count());
        }
    }
}
