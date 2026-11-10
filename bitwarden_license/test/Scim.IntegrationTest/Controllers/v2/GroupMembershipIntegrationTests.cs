using System.Diagnostics;
using System.Text.Json;
using Bit.Core.Enums;
using Bit.Scim.IntegrationTest.Factories;
using Bit.Scim.Models;
using Xunit;
using Xunit.Abstractions;

namespace Bit.Scim.IntegrationTest.Controllers.v2;

public class GroupMembershipIntegrationTests : IClassFixture<ScimApplicationFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ScimApplicationFactory _factory;
    private readonly ITestOutputHelper _testOutputHelper;

    public GroupMembershipIntegrationTests(ScimApplicationFactory factory, ITestOutputHelper testOutputHelper)
    {
        _factory = factory;
        _testOutputHelper = testOutputHelper;
    }

    public Task InitializeAsync()
    {
        _factory.ReinitializeDbForTests(_factory.GetDatabaseContext());
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetGroup_ReturnsMemberReferences()
    {
        var databaseContext = _factory.GetDatabaseContext();
        Assert.Equal(ScimApplicationFactory.TestOrganizationId1,
            databaseContext.Groups.Single(group => group.Id == ScimApplicationFactory.TestGroupId1).OrganizationId);
        var response = await _factory.GroupsGetAsync(
            ScimApplicationFactory.TestOrganizationId1,
            ScimApplicationFactory.TestGroupId1);

        Assert.Equal(StatusCodes.Status200OK, response.Response.StatusCode);

        using var document = await JsonDocument.ParseAsync(response.Response.Body);
        var member = document.RootElement.GetProperty("members")
            .EnumerateArray()
            .Single(candidate => candidate.GetProperty("value").GetString() == ScimApplicationFactory.TestOrganizationUserId1.ToString());
        Assert.Equal(ScimApplicationFactory.TestOrganizationUserId1.ToString(), member.GetProperty("value").GetString());
        Assert.Equal("user1@example.com", member.GetProperty("display").GetString());
        Assert.Equal("User", member.GetProperty("type").GetString());
        Assert.Equal(
            $"http://localhost/v2/{ScimApplicationFactory.TestOrganizationId1}/Users/{ScimApplicationFactory.TestOrganizationUserId1}",
            member.GetProperty("$ref").GetString());
    }

    [Fact]
    public async Task MembershipEndpoints_HandleOrganizationScale()
    {
        const int organizationUserCount = 8_000;
        const int activeUserCount = 1_400;

        var databaseContext = _factory.GetDatabaseContext();
        var generatedUsers = Enumerable.Range(0, organizationUserCount - 4)
            .Select(index =>
            {
                var userId = Guid.NewGuid();
                var organizationUserId = Guid.NewGuid();
                var isActive = index < activeUserCount - 3;
                var email = $"{(isActive ? "a-active" : "z-revoked")}-{index:D4}@example.com";

                return new
                {
                    User = new Bit.Infrastructure.EntityFramework.Models.User
                    {
                        Id = userId,
                        Name = email,
                        Email = email,
                        ApiKey = string.Empty,
                        SecurityStamp = string.Empty
                    },
                    OrganizationUser = new Bit.Infrastructure.EntityFramework.Models.OrganizationUser
                    {
                        Id = organizationUserId,
                        OrganizationId = ScimApplicationFactory.TestOrganizationId1,
                        UserId = userId,
                        Email = email,
                        Status = isActive ? OrganizationUserStatusType.Confirmed : OrganizationUserStatusType.Revoked
                    },
                    GroupUser = isActive
                        ? new Bit.Infrastructure.EntityFramework.Models.GroupUser
                        {
                            GroupId = ScimApplicationFactory.TestGroupId1,
                            OrganizationUserId = organizationUserId
                        }
                        : null
                };
            })
            .ToArray();

        databaseContext.Users.AddRange(generatedUsers.Select(item => item.User));
        databaseContext.OrganizationUsers.AddRange(generatedUsers.Select(item => item.OrganizationUser));
        databaseContext.GroupUsers.AddRange(
            generatedUsers.Where(item => item.GroupUser != null).Select(item => item.GroupUser!));
        databaseContext.GroupUsers.Add(new Bit.Infrastructure.EntityFramework.Models.GroupUser
        {
            GroupId = ScimApplicationFactory.TestGroupId1,
            OrganizationUserId = ScimApplicationFactory.TestOrganizationUserId2
        });
        databaseContext.SaveChanges();
        Assert.Equal(organizationUserCount, databaseContext.OrganizationUsers.Count(
            organizationUser => organizationUser.OrganizationId == ScimApplicationFactory.TestOrganizationId1));
        Assert.Equal(activeUserCount, databaseContext.OrganizationUsers.Count(
            organizationUser =>
                organizationUser.OrganizationId == ScimApplicationFactory.TestOrganizationId1 &&
                organizationUser.Status == OrganizationUserStatusType.Confirmed));

        var userListStopwatch = Stopwatch.StartNew();
        var userListResponse = await _factory.UsersGetListAsync(
            ScimApplicationFactory.TestOrganizationId1,
            filter: null,
            count: 50,
            startIndex: 1,
            groupCount: 50);
        userListStopwatch.Stop();

        Assert.Equal(StatusCodes.Status200OK, userListResponse.Response.StatusCode);
        var userListResponseBody = await ReadResponseBodyAsync(userListResponse.Response.Body);
        var userList = JsonSerializer.Deserialize<ScimListResponseModel<ScimUserResponseModel>>(
            userListResponseBody,
            _jsonOptions);
        Assert.NotNull(userList);
        Assert.Equal(organizationUserCount, userList.TotalResults);
        Assert.Equal(50, userList.Resources.Count);
        Assert.All(userList.Resources, user =>
        {
            Assert.True(user.Active);
            AssertGroupReference(Assert.Single(user.Groups), ScimApplicationFactory.TestGroupId1, "Test Group 1");
        });

        var groupGetStopwatch = Stopwatch.StartNew();
        var groupResponse = await _factory.GroupsGetAsync(
            ScimApplicationFactory.TestOrganizationId1,
            ScimApplicationFactory.TestGroupId1,
            memberCount: activeUserCount);
        groupGetStopwatch.Stop();

        Assert.Equal(StatusCodes.Status200OK, groupResponse.Response.StatusCode);
        var groupResponseBody = await ReadResponseBodyAsync(groupResponse.Response.Body);
        var groupResponseBytes = groupResponseBody.Length;
        var group = JsonSerializer.Deserialize<ScimGroupResponseModel>(groupResponseBody, _jsonOptions);
        Assert.NotNull(group);
        Assert.Equal(activeUserCount, group.Members.Count);
        Assert.All(group.Members, member =>
        {
            Assert.Equal("User", member.Type);
            Assert.StartsWith(
                $"http://localhost/v2/{ScimApplicationFactory.TestOrganizationId1}/Users/",
                member.Ref);
        });

        var groupListStopwatch = Stopwatch.StartNew();
        var groupListResponse = await _factory.GroupsGetListAsync(
            ScimApplicationFactory.TestOrganizationId1,
            filter: null,
            count: 3,
            startIndex: 1,
            memberCount: activeUserCount);
        groupListStopwatch.Stop();

        Assert.Equal(StatusCodes.Status200OK, groupListResponse.Response.StatusCode);
        var groupListResponseBody = await ReadResponseBodyAsync(groupListResponse.Response.Body);
        var groupList = JsonSerializer.Deserialize<ScimListResponseModel<ScimGroupResponseModel>>(
            groupListResponseBody,
            _jsonOptions);
        Assert.NotNull(groupList);
        Assert.Equal(3, groupList.TotalResults);
        Assert.Equal(3, groupList.Resources.Count);
        var groupWithLargeMembership = Assert.Single(
            groupList.Resources,
            resource => resource.Id == ScimApplicationFactory.TestGroupId1);
        Assert.Equal(activeUserCount, groupWithLargeMembership.Members.Count);
        var groupListResponseBytes = groupListResponseBody.Length;

        _testOutputHelper.WriteLine(
            $"8,000 organization users (1,400 active): GET /Users returned {userList.Resources.Count} records " +
            $"({userListResponseBody.Length} bytes) " +
            $"in {userListStopwatch.ElapsedMilliseconds} ms; GET /Groups/{{id}} returned " +
            $"{group.Members.Count} members ({groupResponseBytes} bytes) in {groupGetStopwatch.ElapsedMilliseconds} ms; " +
            $"GET /Groups returned {groupWithLargeMembership.Members.Count} members " +
            $"({groupListResponseBytes} bytes) in {groupListStopwatch.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public async Task GetUserAndUserList_ReturnPaginatedGroupReferences()
    {
        var databaseContext = _factory.GetDatabaseContext();
        databaseContext.GroupUsers.AddRange(
            new Bit.Infrastructure.EntityFramework.Models.GroupUser
            {
                GroupId = ScimApplicationFactory.TestGroupId2,
                OrganizationUserId = ScimApplicationFactory.TestOrganizationUserId1
            },
            new Bit.Infrastructure.EntityFramework.Models.GroupUser
            {
                GroupId = ScimApplicationFactory.TestGroupId3,
                OrganizationUserId = ScimApplicationFactory.TestOrganizationUserId1
            });
        databaseContext.SaveChanges();

        var detailResponse = await _factory.UsersGetAsync(
            ScimApplicationFactory.TestOrganizationId1,
            ScimApplicationFactory.TestOrganizationUserId1,
            groupStartIndex: 2,
            groupCount: 1);
        Assert.Equal(StatusCodes.Status200OK, detailResponse.Response.StatusCode);

        var detail = JsonSerializer.Deserialize<ScimUserResponseModel>(detailResponse.Response.Body, _jsonOptions);
        Assert.NotNull(detail);
        Assert.Collection(detail.Groups,
            group => AssertGroupReference(group, ScimApplicationFactory.TestGroupId2, "Test Group 2"));

        var listResponse = await _factory.UsersGetListAsync(
            ScimApplicationFactory.TestOrganizationId1,
            "userName eq user1@example.com",
            count: 10,
            startIndex: 1,
            groupStartIndex: 3,
            groupCount: 1);
        Assert.Equal(StatusCodes.Status200OK, listResponse.Response.StatusCode);

        var list = JsonSerializer.Deserialize<ScimListResponseModel<ScimUserResponseModel>>(listResponse.Response.Body, _jsonOptions);
        Assert.NotNull(list);
        var user = Assert.Single(list.Resources);
        Assert.Collection(user.Groups,
            group => AssertGroupReference(group, ScimApplicationFactory.TestGroupId1, "Test Group 1"));

        var outOfRangeResponse = await _factory.UsersGetAsync(
            ScimApplicationFactory.TestOrganizationId1,
            ScimApplicationFactory.TestOrganizationUserId1,
            groupStartIndex: 4,
            groupCount: 1);
        var userWithOutOfRangeGroups = JsonSerializer.Deserialize<ScimUserResponseModel>(
            outOfRangeResponse.Response.Body,
            _jsonOptions);
        Assert.Empty(userWithOutOfRangeGroups.Groups);

        var noGroupsResponse = await _factory.UsersGetAsync(
            ScimApplicationFactory.TestOrganizationId1,
            ScimApplicationFactory.TestOrganizationUserId2,
            groupStartIndex: 2,
            groupCount: 1);
        var userWithoutGroups = JsonSerializer.Deserialize<ScimUserResponseModel>(noGroupsResponse.Response.Body, _jsonOptions);
        Assert.Empty(userWithoutGroups.Groups);
    }

    [Fact]
    public async Task GetGroupAndGroupList_ReturnPaginatedMemberReferences()
    {
        var detailResponse = await _factory.GroupsGetAsync(
            ScimApplicationFactory.TestOrganizationId1,
            ScimApplicationFactory.TestGroupId1,
            memberStartIndex: 2,
            memberCount: 1);
        Assert.Equal(StatusCodes.Status200OK, detailResponse.Response.StatusCode);

        var detail = JsonSerializer.Deserialize<ScimGroupResponseModel>(detailResponse.Response.Body, _jsonOptions);
        Assert.NotNull(detail);
        Assert.Collection(detail.Members,
            member => AssertMemberReference(member, ScimApplicationFactory.TestOrganizationUserId1, "user1@example.com"));

        var listResponse = await _factory.GroupsGetListAsync(
            ScimApplicationFactory.TestOrganizationId1,
            filter: null,
            count: 3,
            startIndex: 1,
            memberStartIndex: 2,
            memberCount: 1);
        Assert.Equal(StatusCodes.Status200OK, listResponse.Response.StatusCode);

        var list = JsonSerializer.Deserialize<ScimListResponseModel<ScimGroupResponseModel>>(listResponse.Response.Body, _jsonOptions);
        Assert.NotNull(list);
        var group = Assert.Single(list.Resources, resource => resource.Id == ScimApplicationFactory.TestGroupId1);
        Assert.Collection(group.Members,
            member => AssertMemberReference(member, ScimApplicationFactory.TestOrganizationUserId1, "user1@example.com"));

        var outOfRangeResponse = await _factory.GroupsGetAsync(
            ScimApplicationFactory.TestOrganizationId1,
            ScimApplicationFactory.TestGroupId1,
            memberStartIndex: 3,
            memberCount: 1);
        var groupWithoutPageMembers = JsonSerializer.Deserialize<ScimGroupResponseModel>(
            outOfRangeResponse.Response.Body,
            _jsonOptions);
        Assert.Empty(groupWithoutPageMembers.Members);
    }

    [Fact]
    public async Task PatchGroup_AddReplaceAndRemoveMembers_UpdatesBothResources()
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        var groupId = ScimApplicationFactory.TestGroupId2;

        var addResponse = await _factory.GroupsPatchAsync(
            organizationId,
            groupId,
            CreatePatch("add", "members",
                $"[{{\"value\":\"{ScimApplicationFactory.TestOrganizationUserId2}\"}}," +
                $"{{\"value\":\"{ScimApplicationFactory.TestOrganizationUserId3}\"}}]"));
        Assert.Equal(StatusCodes.Status204NoContent, addResponse.Response.StatusCode);

        var afterAdd = await GetGroupAsync(organizationId, groupId);
        Assert.Equal(
            new[] { ScimApplicationFactory.TestOrganizationUserId2, ScimApplicationFactory.TestOrganizationUserId3 }
                .OrderBy(id => id),
            afterAdd.Members.Select(member => Guid.Parse(member.Value)).OrderBy(id => id));
        await AssertUserHasGroupAsync(organizationId, ScimApplicationFactory.TestOrganizationUserId2, groupId, "Test Group 2");
        await AssertUserHasGroupAsync(organizationId, ScimApplicationFactory.TestOrganizationUserId3, groupId, "Test Group 2");

        var duplicateAddResponse = await _factory.GroupsPatchAsync(
            organizationId,
            groupId,
            CreatePatch("add", "members",
                $"[{{\"value\":\"{ScimApplicationFactory.TestOrganizationUserId2}\"}}]"));
        Assert.Equal(StatusCodes.Status204NoContent, duplicateAddResponse.Response.StatusCode);
        Assert.Equal(2, (await GetGroupAsync(organizationId, groupId)).Members.Count);

        var replaceResponse = await _factory.GroupsPatchAsync(
            organizationId,
            groupId,
            CreatePatch("replace", "members",
                $"[{{\"value\":\"{ScimApplicationFactory.TestOrganizationUserId1}\"}}]"));
        Assert.Equal(StatusCodes.Status204NoContent, replaceResponse.Response.StatusCode);
        Assert.Collection((await GetGroupAsync(organizationId, groupId)).Members,
            member => AssertMemberReference(
                member,
                ScimApplicationFactory.TestOrganizationUserId1,
                "user1@example.com"));
        await AssertUserHasGroupAsync(organizationId, ScimApplicationFactory.TestOrganizationUserId1, groupId, "Test Group 2");
        await AssertUserHasNoGroupAsync(organizationId, ScimApplicationFactory.TestOrganizationUserId2, groupId);
        await AssertUserHasNoGroupAsync(organizationId, ScimApplicationFactory.TestOrganizationUserId3, groupId);

        var removeResponse = await _factory.GroupsPatchAsync(
            organizationId,
            groupId,
            CreatePatch(
                "remove",
                $"members[value eq \"{ScimApplicationFactory.TestOrganizationUserId1}\"]",
                "{}"));
        Assert.Equal(StatusCodes.Status204NoContent, removeResponse.Response.StatusCode);
        Assert.Empty((await GetGroupAsync(organizationId, groupId)).Members);
        await AssertUserHasNoGroupAsync(organizationId, ScimApplicationFactory.TestOrganizationUserId1, groupId);
    }

    [Fact]
    public async Task PatchGroup_MultipleOperationsAreAppliedInOrder()
    {
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        var groupId = ScimApplicationFactory.TestGroupId2;
        var response = await _factory.GroupsPatchAsync(
            organizationId,
            groupId,
            new ScimPatchModel
            {
                Operations =
                [
                    new ScimPatchModel.OperationModel
                    {
                        Op = "add",
                        Path = "members",
                        Value = JsonDocument.Parse(
                            $"[{{\"value\":\"{ScimApplicationFactory.TestOrganizationUserId2}\"}}," +
                            $"{{\"value\":\"{ScimApplicationFactory.TestOrganizationUserId3}\"}}]")
                            .RootElement
                    },
                    new ScimPatchModel.OperationModel
                    {
                        Op = "remove",
                        Path = $"members[value eq \"{ScimApplicationFactory.TestOrganizationUserId2}\"]",
                        Value = JsonDocument.Parse("{}").RootElement
                    }
                ],
                Schemas = ["urn:ietf:params:scim:api:messages:2.0:PatchOp"]
            });

        Assert.Equal(StatusCodes.Status204NoContent, response.Response.StatusCode);
        Assert.Collection((await GetGroupAsync(organizationId, groupId)).Members,
            member => AssertMemberReference(
                member,
                ScimApplicationFactory.TestOrganizationUserId3,
                "user3@example.com"));
        await AssertUserHasNoGroupAsync(organizationId, ScimApplicationFactory.TestOrganizationUserId2, groupId);
        await AssertUserHasGroupAsync(organizationId, ScimApplicationFactory.TestOrganizationUserId3, groupId, "Test Group 2");
    }

    [Fact]
    public async Task PatchUser_GroupsPath_ReturnsBadRequestBecauseAttributeIsReadOnly()
    {
        var response = await _factory.UsersPatchAsync(
            ScimApplicationFactory.TestOrganizationId1,
            ScimApplicationFactory.TestOrganizationUserId2,
            CreatePatch("add", "groups",
                $"[{{\"value\":\"{ScimApplicationFactory.TestGroupId2}\"}}]"));

        Assert.Equal(StatusCodes.Status400BadRequest, response.Response.StatusCode);
        var error = JsonSerializer.Deserialize<ScimErrorResponseModel>(response.Response.Body, _jsonOptions);
        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.Status);
        Assert.Contains("read-only", error.Detail);

        var user = await GetUserAsync(
            ScimApplicationFactory.TestOrganizationId1,
            ScimApplicationFactory.TestOrganizationUserId2);
        Assert.Empty(user.Groups);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("replace")]
    public async Task PatchGroup_MemberFromAnotherOrganization_ReturnsBadRequest(string operation)
    {
        var foreignOrganizationId = Guid.NewGuid();
        var foreignUserId = Guid.NewGuid();
        var foreignOrganizationUserId = Guid.NewGuid();
        var databaseContext = _factory.GetDatabaseContext();
        databaseContext.Organizations.Add(new Bit.Infrastructure.EntityFramework.AdminConsole.Models.Organization
        {
            Id = foreignOrganizationId,
            Name = "Foreign Organization",
            BillingEmail = $"billing-{foreignOrganizationId}@example.com",
            Plan = "Enterprise",
            UseGroups = true
        });
        databaseContext.Users.Add(new Bit.Infrastructure.EntityFramework.Models.User
        {
            Id = foreignUserId,
            Email = $"foreign-{foreignUserId}@example.com",
            ApiKey = string.Empty,
            SecurityStamp = string.Empty
        });
        databaseContext.OrganizationUsers.Add(new Bit.Infrastructure.EntityFramework.Models.OrganizationUser
        {
            Id = foreignOrganizationUserId,
            OrganizationId = foreignOrganizationId,
            UserId = foreignUserId,
            Email = $"foreign-{foreignUserId}@example.com",
            Status = OrganizationUserStatusType.Confirmed
        });
        databaseContext.SaveChanges();

        var response = await _factory.GroupsPatchAsync(
            ScimApplicationFactory.TestOrganizationId1,
            ScimApplicationFactory.TestGroupId1,
            CreatePatch(operation, "members", $"[{{\"value\":\"{foreignOrganizationUserId}\"}}]"));

        Assert.Equal(StatusCodes.Status400BadRequest, response.Response.StatusCode);
        var group = await GetGroupAsync(
            ScimApplicationFactory.TestOrganizationId1,
            ScimApplicationFactory.TestGroupId1);
        Assert.Equal(
            new[] { ScimApplicationFactory.TestOrganizationUserId1, ScimApplicationFactory.TestOrganizationUserId4 }
                .OrderBy(id => id),
            group.Members.Select(member => Guid.Parse(member.Value)).OrderBy(id => id));
    }

    private async Task<ScimGroupResponseModel> GetGroupAsync(Guid organizationId, Guid groupId)
    {
        var response = await _factory.GroupsGetAsync(organizationId, groupId);
        Assert.Equal(StatusCodes.Status200OK, response.Response.StatusCode);
        return JsonSerializer.Deserialize<ScimGroupResponseModel>(response.Response.Body, _jsonOptions)!;
    }

    private async Task<ScimUserResponseModel> GetUserAsync(Guid organizationId, Guid organizationUserId)
    {
        var response = await _factory.UsersGetAsync(organizationId, organizationUserId);
        Assert.Equal(StatusCodes.Status200OK, response.Response.StatusCode);
        return JsonSerializer.Deserialize<ScimUserResponseModel>(response.Response.Body, _jsonOptions)!;
    }

    private async Task AssertUserHasGroupAsync(
        Guid organizationId,
        Guid organizationUserId,
        Guid groupId,
        string displayName)
    {
        var user = await GetUserAsync(organizationId, organizationUserId);
        var group = Assert.Single(user.Groups, reference => reference.Value == groupId.ToString());
        AssertGroupReference(group, groupId, displayName);
    }

    private async Task AssertUserHasNoGroupAsync(Guid organizationId, Guid organizationUserId, Guid groupId)
    {
        var user = await GetUserAsync(organizationId, organizationUserId);
        Assert.DoesNotContain(user.Groups, reference => reference.Value == groupId.ToString());
    }

    private static ScimPatchModel CreatePatch(string operation, string path, string value)
    {
        return new ScimPatchModel
        {
            Operations =
            [
                new ScimPatchModel.OperationModel
                {
                    Op = operation,
                    Path = path,
                    Value = JsonDocument.Parse(value).RootElement
                }
            ],
            Schemas = ["urn:ietf:params:scim:api:messages:2.0:PatchOp"]
        };
    }

    private static async Task<byte[]> ReadResponseBodyAsync(Stream responseBody)
    {
        using var buffer = new MemoryStream();
        await responseBody.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private static void AssertGroupReference(
        BaseScimUserModel.UserGroupReference group,
        Guid groupId,
        string displayName)
    {
        Assert.Equal(groupId.ToString(), group.Value);
        Assert.Equal(displayName, group.Display);
        Assert.Equal("direct", group.Type);
        Assert.Equal(
            $"http://localhost/v2/{ScimApplicationFactory.TestOrganizationId1}/Groups/{groupId}",
            group.Ref);
    }

    private static void AssertMemberReference(
        ScimGroupResponseModel.GroupMemberReference member,
        Guid organizationUserId,
        string displayName)
    {
        Assert.Equal(organizationUserId.ToString(), member.Value);
        Assert.Equal(displayName, member.Display);
        Assert.Equal("User", member.Type);
        Assert.Equal(
            $"http://localhost/v2/{ScimApplicationFactory.TestOrganizationId1}/Users/{organizationUserId}",
            member.Ref);
    }
}
