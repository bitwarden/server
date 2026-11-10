using System.Text.Json;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.IntegrationTestCommon.Factories;
using Bit.Scim.Controllers.v2;
using Bit.Scim.Groups.Interfaces;
using Bit.Scim.IntegrationTest.Factories;
using Bit.Scim.Models;
using Bit.Scim.Users.Interfaces;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Bit.Scim.IntegrationTest.Controllers.v2;

public class AttributeProjectionTests : IClassFixture<ScimApplicationFactory>
{
    private readonly ScimApplicationFactory _factory;

    public AttributeProjectionTests(ScimApplicationFactory factory)
    {
        _factory = factory;
        using var scope = factory.Services.CreateScope();
        factory.ReinitializeDbForTests(scope.ServiceProvider.GetRequiredService<Bit.Infrastructure.EntityFramework.Repositories.DatabaseContext>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Users_SelectNestedAttributes_PreservesRequiredFields(bool list)
    {
        var path = list ? "Users" : $"Users/{ScimApplicationFactory.TestOrganizationUserId1}";
        var response = await _factory.Server.GetAsync(
            $"/v2/{ScimApplicationFactory.TestOrganizationId1}/{path}",
            context => context.Request.QueryString = new QueryString("?attributes=USERNAME,name.givenName,groups.value"));
        Assert.Equal(200, response.Response.StatusCode);
        using var document = await JsonDocument.ParseAsync(response.Response.Body);
        var root = document.RootElement;
        if (list)
        {
            Assert.True(root.TryGetProperty("totalResults", out _));
            root = root.GetProperty("resources")[0];
        }
        Assert.True(root.TryGetProperty("id", out _));
        Assert.True(root.TryGetProperty("schemas", out _));
        Assert.True(root.TryGetProperty("userName", out _));
        Assert.False(root.TryGetProperty("meta", out _));
        Assert.False(root.TryGetProperty("emails", out _));
        Assert.Equal(new[] { "givenName" }, root.GetProperty("name").EnumerateObject().Select(property => property.Name));
        foreach (var group in root.GetProperty("groups").EnumerateArray())
        {
            Assert.Equal(new[] { "value" }, group.EnumerateObject().Select(property => property.Name));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Groups_SelectMemberValues_PreservesMembershipPagination(bool list)
    {
        var path = list ? "Groups" : $"Groups/{ScimApplicationFactory.TestGroupId1}";
        var response = await _factory.Server.GetAsync(
            $"/v2/{ScimApplicationFactory.TestOrganizationId1}/{path}",
            context => context.Request.QueryString = new QueryString("?attributes=members.value&memberCount=1"));
        Assert.Equal(200, response.Response.StatusCode);
        using var document = await JsonDocument.ParseAsync(response.Response.Body);
        var root = document.RootElement;
        if (list)
        {
            root = root.GetProperty("resources").EnumerateArray()
                .Single(group => group.GetProperty("id").GetGuid() == ScimApplicationFactory.TestGroupId1);
        }
        Assert.True(root.TryGetProperty("schemas", out _));
        Assert.False(root.TryGetProperty("displayName", out _));
        var member = Assert.Single(root.GetProperty("members").EnumerateArray());
        Assert.Equal(new[] { "value" }, member.EnumerateObject().Select(property => property.Name));
    }

    [Theory]
    [InlineData("Users", "groups")]
    [InlineData("Groups", "members")]
    public async Task Exclusion_PreservesEnvelopeAndRequiredAttributes(string resource, string membership)
    {
        var response = await _factory.Server.GetAsync(
            $"/v2/{ScimApplicationFactory.TestOrganizationId1}/{resource}",
            context => context.Request.QueryString = new QueryString($"?excludedAttributes={membership},meta,id,schemas"));
        Assert.Equal(200, response.Response.StatusCode);
        using var document = await JsonDocument.ParseAsync(response.Response.Body);
        Assert.Equal(50, document.RootElement.GetProperty("itemsPerPage").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("startIndex").GetInt32());
        Assert.NotEmpty(document.RootElement.GetProperty("schemas").EnumerateArray());
        foreach (var item in document.RootElement.GetProperty("resources").EnumerateArray())
        {
            Assert.True(item.TryGetProperty("id", out _));
            Assert.True(item.TryGetProperty("schemas", out _));
            Assert.False(item.TryGetProperty(membership, out _));
            Assert.False(item.TryGetProperty("meta", out _));
        }
    }

    [Theory]
    [InlineData("Users", "?attributes=id&excludedAttributes=groups")]
    [InlineData("Groups", "?attributes=id&excludedAttributes=members")]
    [InlineData("Users", "?attributes=")]
    [InlineData("Groups", "?excludedAttributes=members,,meta")]
    [InlineData("Users", "?attributes=name..givenName")]
    [InlineData("Users", "?attributes=1invalid")]
    [InlineData("Groups", "?attributes=members.$invalid")]
    [InlineData("Users", "?attributes=urn:ietf:params:scim:schemas:core:2.0:User:")]
    public async Task InvalidSelection_ReturnsScimError(string resource, string query)
    {
        var response = await _factory.Server.GetAsync(
            $"/v2/{ScimApplicationFactory.TestOrganizationId1}/{resource}",
            context => context.Request.QueryString = new QueryString(query));
        Assert.Equal(400, response.Response.StatusCode);
        using var document = await JsonDocument.ParseAsync(response.Response.Body);
        Assert.Equal(400, document.RootElement.GetProperty("status").GetInt32());
        Assert.Contains("urn:ietf:params:scim:api:messages:2.0:Error",
            document.RootElement.GetProperty("schemas").EnumerateArray().Select(value => value.GetString()));
    }

    [Theory]
    [InlineData("attributes=unknown,name,name.givenName,NAME")]
    [InlineData("attributes=urn:ietf:params:scim:schemas:core:2.0:User:name")]
    [InlineData("attributes=URN:IETF:PARAMS:SCIM:SCHEMAS:CORE:2.0:USER:name")]
    public async Task ParentSelection_IncludesAllChildrenAndIgnoresUnknownAttributes(string query)
    {
        var response = await _factory.Server.GetAsync(
            $"/v2/{ScimApplicationFactory.TestOrganizationId1}/Users/{ScimApplicationFactory.TestOrganizationUserId1}",
            context => context.Request.QueryString = new QueryString("?" + query));
        Assert.Equal(200, response.Response.StatusCode);
        using var document = await JsonDocument.ParseAsync(response.Response.Body);
        Assert.Equal(4, document.RootElement.GetProperty("name").EnumerateObject().Count());
        Assert.False(document.RootElement.TryGetProperty("unknown", out _));
    }

    [Theory]
    [InlineData("attributes=urn:ietf:params:scim:schemas:core:2.0:Group:displayName", true)]
    [InlineData("excludedAttributes=URN:IETF:PARAMS:SCIM:SCHEMAS:CORE:2.0:GROUP:displayName", false)]
    public async Task GroupSchemaQualifiedSelection_PreservesPrefixHandling(string query, bool includesDisplay)
    {
        var response = await _factory.Server.GetAsync(
            $"/v2/{ScimApplicationFactory.TestOrganizationId1}/Groups/{ScimApplicationFactory.TestGroupId1}",
            context => context.Request.QueryString = new QueryString("?" + query));
        Assert.Equal(200, response.Response.StatusCode);
        using var document = await JsonDocument.ParseAsync(response.Response.Body);
        Assert.Equal(includesDisplay, document.RootElement.TryGetProperty("displayName", out _));
        Assert.True(document.RootElement.TryGetProperty("id", out _));
        Assert.True(document.RootElement.TryGetProperty("schemas", out _));
    }

    [Fact]
    public async Task NestedExclusion_RemovesOnlySelectedSubattribute()
    {
        var response = await _factory.Server.GetAsync(
            $"/v2/{ScimApplicationFactory.TestOrganizationId1}/Users/{ScimApplicationFactory.TestOrganizationUserId1}",
            context => context.Request.QueryString = new QueryString("?excludedAttributes=groups.display,name.givenName"));
        using var document = await JsonDocument.ParseAsync(response.Response.Body);
        Assert.Equal(200, response.Response.StatusCode);
        Assert.False(document.RootElement.GetProperty("name").TryGetProperty("givenName", out _));
        var group = Assert.Single(document.RootElement.GetProperty("groups").EnumerateArray());
        Assert.False(group.TryGetProperty("display", out _));
        Assert.True(group.TryGetProperty("value", out _));
    }

    [Fact]
    public async Task ExcludedMemberships_SkipRepositoryCalls()
    {
        using var scope = _factory.Services.CreateScope();
        var groupRepository = Substitute.For<IGroupRepository>();
        var organizationUsers = Substitute.For<IOrganizationUserRepository>();
        var group = new Bit.Core.AdminConsole.Entities.Group
        {
            Id = ScimApplicationFactory.TestGroupId1,
            OrganizationId = ScimApplicationFactory.TestOrganizationId1,
            Name = "Projected group"
        };
        groupRepository.GetByIdAsync(group.Id).Returns(group);
        var controller = ActivatorUtilities.CreateInstance<GroupsController>(scope.ServiceProvider,
            groupRepository, organizationUsers);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider }
        };
        controller.Request.QueryString = new QueryString("?excludedAttributes=members");
        await controller.Get(group.OrganizationId, group.Id, new GetGroupsQueryParamModel());
        await groupRepository.DidNotReceive().GetManyUserIdsByIdAsync(Arg.Any<Guid>());
        await organizationUsers.DidNotReceive().GetManyAsync(Arg.Any<IEnumerable<Guid>>());

        var realUsers = scope.ServiceProvider.GetRequiredService<IOrganizationUserRepository>();
        var detail = await realUsers.GetDetailsByIdAsync(ScimApplicationFactory.TestOrganizationUserId1);
        organizationUsers.GetDetailsByIdAsync(ScimApplicationFactory.TestOrganizationUserId1).Returns(detail);
        var usersController = ActivatorUtilities.CreateInstance<UsersController>(scope.ServiceProvider,
            organizationUsers, groupRepository);
        usersController.ControllerContext = controller.ControllerContext;
        usersController.Request.QueryString = new QueryString("?attributes=userName");
        await usersController.Get(ScimApplicationFactory.TestOrganizationId1, ScimApplicationFactory.TestOrganizationUserId1);
        await organizationUsers.DidNotReceive().GetManyGroupIdsByUserIdAsync(Arg.Any<Guid>());

        var usersQuery = Substitute.For<IGetUsersListQuery>();
        usersQuery.GetUsersListAsync(ScimApplicationFactory.TestOrganizationId1, Arg.Any<GetUsersQueryParamModel>())
            .Returns((new[] { detail }, 1));
        usersController = ActivatorUtilities.CreateInstance<UsersController>(scope.ServiceProvider,
            organizationUsers, groupRepository, usersQuery);
        usersController.ControllerContext = controller.ControllerContext;
        await usersController.Get(ScimApplicationFactory.TestOrganizationId1, new GetUsersQueryParamModel());
        await organizationUsers.DidNotReceive().GetManyGroupIdsByUserIdAsync(Arg.Any<Guid>());
        await groupRepository.DidNotReceive().GetManyGroupUsersByOrganizationUserIdsAsync(
            Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>());

        var groupsQuery = Substitute.For<IGetGroupsListQuery>();
        groupsQuery.GetGroupsListAsync(group.OrganizationId, Arg.Any<GetGroupsQueryParamModel>())
            .Returns((new[] { group }, 1));
        controller = ActivatorUtilities.CreateInstance<GroupsController>(scope.ServiceProvider,
            groupRepository, organizationUsers, groupsQuery);
        controller.ControllerContext = usersController.ControllerContext;
        controller.Request.QueryString = new QueryString("?excludedAttributes=members");
        await controller.Get(group.OrganizationId, new GetGroupsQueryParamModel());
        await groupRepository.DidNotReceive().GetManyUserIdsByIdAsync(Arg.Any<Guid>());
        await groupRepository.DidNotReceive().GetManyGroupUsersByGroupIdsAsync(
            Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>());
    }

    [Theory]
    [InlineData("?attributes=members.value")]
    [InlineData("?excludedAttributes=members.display")]
    public async Task MemberSelectionWithoutDisplay_SkipsDetailsAndEmailQueries(string query)
    {
        using var scope = _factory.Services.CreateScope();
        var groups = Substitute.For<IGroupRepository>();
        var organizationUsers = Substitute.For<IOrganizationUserRepository>();
        var users = Substitute.For<IUserRepository>();
        var group = new Bit.Core.AdminConsole.Entities.Group
        {
            Id = ScimApplicationFactory.TestGroupId1,
            OrganizationId = ScimApplicationFactory.TestOrganizationId1
        };
        groups.GetByIdAsync(group.Id).Returns(group);
        groups.GetManyUserIdsByIdAsync(group.Id).Returns(new[] { ScimApplicationFactory.TestOrganizationUserId1 });
        var controller = ActivatorUtilities.CreateInstance<GroupsController>(scope.ServiceProvider, groups, organizationUsers, users);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider }
        };
        controller.Request.QueryString = new QueryString(query);
        await controller.Get(group.OrganizationId, group.Id, new GetGroupsQueryParamModel());
        await organizationUsers.DidNotReceive().GetManyAsync(Arg.Any<IEnumerable<Guid>>());
        await users.DidNotReceive().GetManyAsync(Arg.Any<IEnumerable<Guid>>());
    }

    [Theory]
    [InlineData("Users", "groups")]
    [InlineData("Groups", "members")]
    public async Task DetailExclusion_OmitsMembershipButPreservesOtherFields(string resource, string membership)
    {
        var id = resource == "Users" ? ScimApplicationFactory.TestOrganizationUserId1 : ScimApplicationFactory.TestGroupId1;
        var response = await _factory.Server.GetAsync(
            $"/v2/{ScimApplicationFactory.TestOrganizationId1}/{resource}/{id}",
            context => context.Request.QueryString = new QueryString($"?excludedAttributes={membership}"));
        Assert.Equal(200, response.Response.StatusCode);
        using var document = await JsonDocument.ParseAsync(response.Response.Body);
        Assert.False(document.RootElement.TryGetProperty(membership, out _));
        Assert.True(document.RootElement.TryGetProperty("meta", out _));
        Assert.Equal(id, document.RootElement.GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData("attributes=groups", "value")]
    [InlineData("attributes=groups.$ref", "$ref")]
    public async Task UserMembershipSelection_RespectsExplicitCount(string query, string required)
    {
        var response = await _factory.Server.GetAsync(
            $"/v2/{ScimApplicationFactory.TestOrganizationId1}/Users/{ScimApplicationFactory.TestOrganizationUserId1}",
            context => context.Request.QueryString = new QueryString("?" + query + "&groupCount=1"));
        Assert.Equal(200, response.Response.StatusCode);
        using var document = await JsonDocument.ParseAsync(response.Response.Body);
        var group = Assert.Single(document.RootElement.GetProperty("groups").EnumerateArray());
        Assert.True(group.TryGetProperty(required, out _));
    }
}
