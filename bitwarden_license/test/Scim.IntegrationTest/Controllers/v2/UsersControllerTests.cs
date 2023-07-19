using System.Text.Json;
using Bit.Core.Enums;
using Bit.Scim.IntegrationTest.Factories;
using Bit.Scim.Models;
using Bit.Scim.Utilities;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Scim.IntegrationTest.Controllers.v2;

public class UsersControllerTests : IClassFixture<ScimApplicationFactory>, IAsyncLifetime
{
    private const int _initialUserCount = 4;

    private readonly ScimApplicationFactory _factory;

    public UsersControllerTests(ScimApplicationFactory factory)
    {
        _factory = factory;
        _factory.DatabaseName = "test_database_users";
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
        var organizationUserId = ScimApplicationFactory.TestOrganizationUserId1;
        var expectedResponse = new ScimUserResponseModel
        {
            Id = ScimApplicationFactory.TestOrganizationUserId1,
            DisplayName = "Test User 1",
            ExternalId = "UA",
            Active = true,
            Emails = new List<BaseScimUserModel.EmailModel>
            {
                new BaseScimUserModel.EmailModel { Primary = true, Type = "work", Value = "user1@example.com" }
            },
            Groups = new List<string>(),
            Name = new BaseScimUserModel.NameModel("Test User 1"),
            UserName = "user1@example.com",
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        var context = await _factory.UsersGetAsync(ScimApplicationFactory.TestOrganizationId1, organizationUserId);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var responseModel = JsonSerializer.Deserialize<ScimUserResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);
    }

    [Fact]
    public async Task Get_NotFound()
    {
        var organizationUserId = Guid.NewGuid();
        var expectedResponse = new ScimErrorResponseModel
        {
            Status = StatusCodes.Status404NotFound,
            Detail = "User not found.",
            Schemas = new List<string> { ScimConstants.Scim2SchemaError }
        };

        var context = await _factory.UsersGetAsync(ScimApplicationFactory.TestOrganizationId1, organizationUserId);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

        var responseModel = JsonSerializer.Deserialize<ScimErrorResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);
    }

    [Fact]
    public async Task GetList_Success()
    {
        string filter = null;
        int? itemsPerPage = 2;
        int? startIndex = 1;
        var expectedResponse = new ScimListResponseModel<ScimUserResponseModel>
        {
            ItemsPerPage = itemsPerPage.Value,
            // Note: total matching results is larger than resources actually returned due to pagination settings. See https://www.rfc-editor.org/rfc/rfc7644#section-3.4.2
            TotalResults = 4,
            StartIndex = startIndex.Value,
            Resources = new List<ScimUserResponseModel>
            {
                new ScimUserResponseModel
                {
                    Id = ScimApplicationFactory.TestOrganizationUserId1,
                    DisplayName = "Test User 1",
                    ExternalId = "UA",
                    Active = true,
                    Emails = new List<BaseScimUserModel.EmailModel>
                    {
                        new BaseScimUserModel.EmailModel { Primary = true, Type = "work", Value = "user1@example.com" }
                    },
                    Groups = new List<string>(),
                    Name = new BaseScimUserModel.NameModel("Test User 1"),
                    UserName = "user1@example.com",
                    Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
                },
                new ScimUserResponseModel
                {
                    Id = ScimApplicationFactory.TestOrganizationUserId2,
                    DisplayName = "Test User 2",
                    ExternalId = "UB",
                    Active = true,
                    Emails = new List<BaseScimUserModel.EmailModel>
                    {
                        new BaseScimUserModel.EmailModel { Primary = true, Type = "work", Value = "user2@example.com" }
                    },
                    Groups = new List<string>(),
                    Name = new BaseScimUserModel.NameModel("Test User 2"),
                    UserName = "user2@example.com",
                    Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaListResponse }
        };

        var context = await _factory.UsersGetListAsync(ScimApplicationFactory.TestOrganizationId1, filter, itemsPerPage, startIndex);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var responseModel = JsonSerializer.Deserialize<ScimListResponseModel<ScimUserResponseModel>>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);
    }

    [Fact]
    public async Task GetList_SearchUserName_Success()
    {
        string filter = "userName eq user2@example.com";
        int? itemsPerPage = 10;
        int? startIndex = 1;
        var expectedResponse = new ScimListResponseModel<ScimUserResponseModel>
        {
            ItemsPerPage = itemsPerPage.Value,
            TotalResults = 1,
            StartIndex = startIndex.Value,
            Resources = new List<ScimUserResponseModel>
            {
                new ScimUserResponseModel
                {
                    Id = ScimApplicationFactory.TestOrganizationUserId2,
                    DisplayName = "Test User 2",
                    ExternalId = "UB",
                    Active = true,
                    Emails = new List<BaseScimUserModel.EmailModel>
                    {
                        new BaseScimUserModel.EmailModel { Primary = true, Type = "work", Value = "user2@example.com" }
                    },
                    Groups = new List<string>(),
                    Name = new BaseScimUserModel.NameModel("Test User 2"),
                    UserName = "user2@example.com",
                    Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaListResponse }
        };

        var context = await _factory.UsersGetListAsync(ScimApplicationFactory.TestOrganizationId1, filter, itemsPerPage, startIndex);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var responseModel = JsonSerializer.Deserialize<ScimListResponseModel<ScimUserResponseModel>>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);
    }

    [Fact]
    public async Task GetList_SearchExternalId_Success()
    {
        string filter = "externalId eq UC";
        int? itemsPerPage = 10;
        int? startIndex = 1;
        var expectedResponse = new ScimListResponseModel<ScimUserResponseModel>
        {
            ItemsPerPage = itemsPerPage.Value,
            TotalResults = 1,
            StartIndex = startIndex.Value,
            Resources = new List<ScimUserResponseModel>
            {
                new ScimUserResponseModel
                {
                    Id = ScimApplicationFactory.TestOrganizationUserId3,
                    DisplayName = "Test User 3",
                    ExternalId = "UC",
                    Active = false,
                    Emails = new List<BaseScimUserModel.EmailModel>
                    {
                        new BaseScimUserModel.EmailModel { Primary = true, Type = "work", Value = "user3@example.com" }
                    },
                    Groups = new List<string>(),
                    Name = new BaseScimUserModel.NameModel("Test User 3"),
                    UserName = "user3@example.com",
                    Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
                }
            },
            Schemas = new List<string> { ScimConstants.Scim2SchemaListResponse }
        };

        var context = await _factory.UsersGetListAsync(ScimApplicationFactory.TestOrganizationId1, filter, itemsPerPage, startIndex);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var responseModel = JsonSerializer.Deserialize<ScimListResponseModel<ScimUserResponseModel>>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);
    }

    [Fact]
    public async Task GetList_EmptyResult_Success()
    {
        string filter = "externalId eq UZ";
        int? itemsPerPage = 10;
        int? startIndex = 1;
        var expectedResponse = new ScimListResponseModel<ScimUserResponseModel>
        {
            ItemsPerPage = itemsPerPage.Value,
            TotalResults = 0,
            StartIndex = startIndex.Value,
            Resources = new List<ScimUserResponseModel>(),
            Schemas = new List<string> { ScimConstants.Scim2SchemaListResponse }
        };

        var context = await _factory.UsersGetListAsync(ScimApplicationFactory.TestOrganizationId1, filter, itemsPerPage, startIndex);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var responseModel = JsonSerializer.Deserialize<ScimListResponseModel<ScimUserResponseModel>>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);
    }

    [Fact]
    public async Task Post_Success()
    {
        var email = "user5@example.com";
        var displayName = "Test User 5";
        var externalId = "UE";
        var inputModel = new ScimUserRequestModel
        {
            Name = new BaseScimUserModel.NameModel(displayName),
            DisplayName = displayName,
            Emails = new List<BaseScimUserModel.EmailModel> { new BaseScimUserModel.EmailModel(email) },
            ExternalId = externalId,
            Active = true,
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };
        var expectedResponse = new ScimUserResponseModel
        {
            // DisplayName is not being saved
            ExternalId = externalId,
            Active = true,
            Emails = new List<BaseScimUserModel.EmailModel>
            {
                new BaseScimUserModel.EmailModel { Primary = true, Type = "work", Value = email }
            },
            Groups = new List<string>(),
            Name = new BaseScimUserModel.NameModel(),
            UserName = email,
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        var context = await _factory.UsersPostAsync(ScimApplicationFactory.TestOrganizationId1, inputModel);

        Assert.Equal(StatusCodes.Status201Created, context.Response.StatusCode);

        // Verifying that the response includes a header with the URL of the created Group
        Assert.Contains(context.Response.Headers, h => h.Key == "Location");

        var responseModel = JsonSerializer.Deserialize<ScimUserResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel, "Id");

        var databaseContext = _factory.GetDatabaseContext();
        Assert.Equal(_initialUserCount + 1, databaseContext.OrganizationUsers.Count());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Post_InvalidEmail_BadRequest(string email)
    {
        var displayName = "Test User 5";
        var externalId = "UE";
        var inputModel = new ScimUserRequestModel
        {
            Name = new BaseScimUserModel.NameModel(displayName),
            DisplayName = displayName,
            Emails = new List<BaseScimUserModel.EmailModel> { new BaseScimUserModel.EmailModel(email) },
            ExternalId = externalId,
            Active = true,
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        var context = await _factory.UsersPostAsync(ScimApplicationFactory.TestOrganizationId1, inputModel);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task Post_Inactive_BadRequest()
    {
        var displayName = "Test User 5";
        var inputModel = new ScimUserRequestModel
        {
            DisplayName = displayName,
            ExternalId = null,
            Active = false,
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        var context = await _factory.UsersPostAsync(ScimApplicationFactory.TestOrganizationId1, inputModel);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("user1@example.com", "UZ")]
    [InlineData("userZ@example.com", "UA")]
    public async Task Post_ExistingData_Conflict(string email, string externalId)
    {
        var inputModel = new ScimUserRequestModel
        {
            DisplayName = "New User",
            Emails = new List<BaseScimUserModel.EmailModel> { new BaseScimUserModel.EmailModel(email) },
            ExternalId = externalId,
            Schemas = null,
            Active = true
        };

        var context = await _factory.UsersPostAsync(ScimApplicationFactory.TestOrganizationId1, inputModel);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);

        var databaseContext = _factory.GetDatabaseContext();
        Assert.Equal(_initialUserCount, databaseContext.OrganizationUsers.Count());
    }

    [Fact]
    public async Task Put_RevokeUser_Success()
    {
        var organizationUserId = ScimApplicationFactory.TestOrganizationUserId2;
        var inputModel = new ScimUserRequestModel
        {
            Active = false
        };
        var expectedResponse = new ScimUserResponseModel
        {
            Id = ScimApplicationFactory.TestOrganizationUserId2,
            DisplayName = "Test User 2",
            ExternalId = "UB",
            Active = false,
            Emails = new List<BaseScimUserModel.EmailModel>
            {
                new BaseScimUserModel.EmailModel { Primary = true, Type = "work", Value = "user2@example.com" }
            },
            Groups = new List<string>(),
            Name = new BaseScimUserModel.NameModel("Test User 2"),
            UserName = "user2@example.com",
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        var context = await _factory.UsersPutAsync(ScimApplicationFactory.TestOrganizationId1, organizationUserId, inputModel);

        var responseModel = JsonSerializer.Deserialize<ScimUserResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);

        var databaseContext = _factory.GetDatabaseContext();
        var revokedUser = databaseContext.OrganizationUsers.FirstOrDefault(g => g.Id == organizationUserId);
        Assert.Equal(OrganizationUserStatusType.Revoked, revokedUser.Status);
    }

    [Fact]
    public async Task Put_RestoreUser_Success()
    {
        var organizationUserId = ScimApplicationFactory.TestOrganizationUserId3;
        var inputModel = new ScimUserRequestModel
        {
            Active = true
        };
        var expectedResponse = new ScimUserResponseModel
        {
            Id = ScimApplicationFactory.TestOrganizationUserId3,
            DisplayName = "Test User 3",
            ExternalId = "UC",
            Active = true,
            Emails = new List<BaseScimUserModel.EmailModel>
            {
                new BaseScimUserModel.EmailModel { Primary = true, Type = "work", Value = "user3@example.com" }
            },
            Groups = new List<string>(),
            Name = new BaseScimUserModel.NameModel("Test User 3"),
            UserName = "user3@example.com",
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        var context = await _factory.UsersPutAsync(ScimApplicationFactory.TestOrganizationId1, organizationUserId, inputModel);

        var responseModel = JsonSerializer.Deserialize<ScimUserResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);

        var databaseContext = _factory.GetDatabaseContext();
        var revokedUser = databaseContext.OrganizationUsers.FirstOrDefault(g => g.Id == organizationUserId);
        Assert.NotEqual(OrganizationUserStatusType.Revoked, revokedUser.Status);
    }

    [Fact]
    public async Task Put_NotFound()
    {
        var organizationUserId = Guid.NewGuid();
        var inputModel = new ScimUserRequestModel
        {
            DisplayName = "Test Group 1",
            ExternalId = "AA",
            Schemas = new List<string>()
        };
        var expectedResponse = new ScimErrorResponseModel
        {
            Status = StatusCodes.Status404NotFound,
            Detail = "User not found.",
            Schemas = new List<string> { ScimConstants.Scim2SchemaError }
        };

        var context = await _factory.UsersPutAsync(ScimApplicationFactory.TestOrganizationId1, organizationUserId, inputModel);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

        var responseModel = JsonSerializer.Deserialize<ScimErrorResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);
    }

    [Fact]
    public async Task Patch_ReplaceRevoke_Success()
    {
        var organizationUserId = ScimApplicationFactory.TestOrganizationUserId2;
        var inputModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>()
            {
                new ScimPatchModel.OperationModel { Op = "replace", Value = JsonDocument.Parse("{\"active\":false}").RootElement  },
            },
            Schemas = new List<string>()
        };

        var context = await _factory.UsersPatchAsync(ScimApplicationFactory.TestOrganizationId1, organizationUserId, inputModel);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);

        var databaseContext = _factory.GetDatabaseContext();

        var organizationUser = databaseContext.OrganizationUsers.FirstOrDefault(g => g.Id == organizationUserId);
        Assert.Equal(OrganizationUserStatusType.Revoked, organizationUser.Status);
    }

    [Fact]
    public async Task Patch_ReplaceRestore_Success()
    {
        var organizationUserId = ScimApplicationFactory.TestOrganizationUserId3;
        var inputModel = new ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>()
            {
                new ScimPatchModel.OperationModel { Op = "replace", Value = JsonDocument.Parse("{\"active\":true}").RootElement  },
            },
            Schemas = new List<string>()
        };

        var context = await _factory.UsersPatchAsync(ScimApplicationFactory.TestOrganizationId1, organizationUserId, inputModel);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);

        var databaseContext = _factory.GetDatabaseContext();

        var organizationUser = databaseContext.OrganizationUsers.FirstOrDefault(g => g.Id == organizationUserId);
        Assert.NotEqual(OrganizationUserStatusType.Revoked, organizationUser.Status);
    }

    [Fact]
    public async Task Patch_NotFound()
    {
        var organizationUserId = Guid.NewGuid();
        var inputModel = new Models.ScimPatchModel
        {
            Operations = new List<ScimPatchModel.OperationModel>(),
            Schemas = new List<string>()
        };
        var expectedResponse = new ScimErrorResponseModel
        {
            Status = StatusCodes.Status404NotFound,
            Detail = "User not found.",
            Schemas = new List<string> { ScimConstants.Scim2SchemaError }
        };

        var context = await _factory.UsersPatchAsync(ScimApplicationFactory.TestOrganizationId1, organizationUserId, inputModel);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

        var responseModel = JsonSerializer.Deserialize<ScimErrorResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);
    }

    [Fact]
    public async Task Delete_Success()
    {
        var organizationUserId = ScimApplicationFactory.TestOrganizationUserId1;
        var inputModel = new ScimUserRequestModel();

        var context = await _factory.UsersDeleteAsync(ScimApplicationFactory.TestOrganizationId1, organizationUserId, inputModel);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);

        var databaseContext = _factory.GetDatabaseContext();
        Assert.Equal(_initialUserCount - 1, databaseContext.OrganizationUsers.Count());
        Assert.False(databaseContext.OrganizationUsers.Any(g => g.Id == organizationUserId));
    }

    [Fact]
    public async Task Delete_NotFound()
    {
        var organizationUserId = Guid.NewGuid();
        var inputModel = new ScimUserRequestModel();
        var expectedResponse = new ScimErrorResponseModel
        {
            Status = StatusCodes.Status404NotFound,
            Detail = "User not found.",
            Schemas = new List<string> { ScimConstants.Scim2SchemaError }
        };

        var context = await _factory.UsersDeleteAsync(ScimApplicationFactory.TestOrganizationId1, organizationUserId, inputModel);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

        var responseModel = JsonSerializer.Deserialize<ScimErrorResponseModel>(context.Response.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        AssertHelper.AssertPropertyEqual(expectedResponse, responseModel);

        var databaseContext = _factory.GetDatabaseContext();
        Assert.Equal(_initialUserCount, databaseContext.OrganizationUsers.Count());
    }
}
