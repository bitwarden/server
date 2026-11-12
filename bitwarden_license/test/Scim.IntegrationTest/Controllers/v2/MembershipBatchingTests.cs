using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.Json;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.IntegrationTestCommon.Factories;
using Bit.Scim.IntegrationTest.Factories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Bit.Scim.IntegrationTest.Controllers.v2;

public class MembershipBatchingTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task GroupList_BatchesMembershipReads(int count)
    {
        var reads = new ReadCounter();
        using var factory = CreateFactory(reads);
        var db = factory.GetDatabaseContext();
        foreach (var groupId in new[] { ScimApplicationFactory.TestGroupId2, ScimApplicationFactory.TestGroupId3 })
        {
            db.GroupUsers.AddRange(
                new GroupUser { GroupId = groupId, OrganizationUserId = ScimApplicationFactory.TestOrganizationUserId1 },
                new GroupUser { GroupId = groupId, OrganizationUserId = ScimApplicationFactory.TestOrganizationUserId4 });
        }
        db.OrganizationUsers.Single(user => user.Id == ScimApplicationFactory.TestOrganizationUserId1).Email = null;
        db.OrganizationUsers.Single(user => user.Id == ScimApplicationFactory.TestOrganizationUserId4).UserId = null;
        db.SaveChanges();
        reads.Commands.Clear();

        var response = await factory.GroupsGetListAsync(
            ScimApplicationFactory.TestOrganizationId1, null, count, 1);

        Assert.Equal(200, response.Response.StatusCode);
        using var document = await JsonDocument.ParseAsync(response.Response.Body);
        var groups = document.RootElement.GetProperty("resources").EnumerateArray().ToArray();
        Assert.Equal(count, groups.Length);
        var members = groups[0].GetProperty("members").EnumerateArray().ToArray();
        Assert.Equal(new[]
        {
            ScimApplicationFactory.TestOrganizationUserId1,
            ScimApplicationFactory.TestOrganizationUserId4
        }.OrderBy(id => id), members.Select(member => member.GetProperty("value").GetGuid()));
        Assert.All(members, member =>
        {
            Assert.Equal(member.GetProperty("value").GetGuid() == ScimApplicationFactory.TestOrganizationUserId1
                ? "user1@example.com" : "user4@example.com", member.GetProperty("display").GetString());
            Assert.Equal("User", member.GetProperty("type").GetString());
            Assert.Equal($"http://localhost/v2/{ScimApplicationFactory.TestOrganizationId1}/Users/{member.GetProperty("value").GetString()}",
                member.GetProperty("$ref").GetString());
        });
        Assert.Single(reads.Commands.Where(command => command.Contains("GroupUser", StringComparison.Ordinal)));
        Assert.InRange(reads.Commands.Count, 1, 6);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task UserList_BatchesMembershipReads(int count)
    {
        var reads = new ReadCounter();
        using var factory = CreateFactory(reads);
        reads.Commands.Clear();

        var response = await factory.UsersGetListAsync(
            ScimApplicationFactory.TestOrganizationId1, null, count, 1);

        Assert.Equal(200, response.Response.StatusCode);
        using var document = await JsonDocument.ParseAsync(response.Response.Body);
        var users = document.RootElement.GetProperty("resources").EnumerateArray().ToArray();
        Assert.Equal(count, users.Length);
        foreach (var user in users)
        {
            var groups = user.GetProperty("groups").EnumerateArray().ToArray();
            var id = user.GetProperty("id").GetGuid();
            if (id == ScimApplicationFactory.TestOrganizationUserId1 || id == ScimApplicationFactory.TestOrganizationUserId4)
            {
                var group = Assert.Single(groups);
                Assert.Equal(ScimApplicationFactory.TestGroupId1, group.GetProperty("value").GetGuid());
                Assert.Equal("Test Group 1", group.GetProperty("display").GetString());
                Assert.Equal("direct", group.GetProperty("type").GetString());
                Assert.Equal($"http://localhost/v2/{ScimApplicationFactory.TestOrganizationId1}/Groups/{ScimApplicationFactory.TestGroupId1}",
                    group.GetProperty("$ref").GetString());
            }
            else
            {
                Assert.Empty(groups);
            }
        }
        Assert.Single(reads.Commands.Where(command => command.Contains("GroupUser", StringComparison.Ordinal)));
        Assert.InRange(reads.Commands.Count, 1, 5);
    }

    [Theory]
    [InlineData("Groups", "members", "memberStartIndex=2&memberCount=1")]
    [InlineData("Users", "groups", "groupStartIndex=2&groupCount=1")]
    [InlineData("Groups", "members", "")]
    [InlineData("Users", "groups", "")]
    public async Task ListMemberships_MatchDetailsWithSharedMemberships(
        string resource, string membership, string query)
    {
        using var factory = CreateFactory(new ReadCounter());
        var db = factory.GetDatabaseContext();
        db.GroupUsers.AddRange(
            new GroupUser
            {
                GroupId = ScimApplicationFactory.TestGroupId2,
                OrganizationUserId = ScimApplicationFactory.TestOrganizationUserId1
            },
            new GroupUser
            {
                GroupId = ScimApplicationFactory.TestGroupId2,
                OrganizationUserId = ScimApplicationFactory.TestOrganizationUserId4
            });
        db.OrganizationUsers.Single(user => user.Id == ScimApplicationFactory.TestOrganizationUserId1).Email = null;
        db.SaveChanges();

        var response = await factory.Server.GetAsync(
            $"/v2/{ScimApplicationFactory.TestOrganizationId1}/{resource}",
            context => context.Request.QueryString = new QueryString("?" + query));
        Assert.Equal(200, response.Response.StatusCode);
        using var document = await JsonDocument.ParseAsync(response.Response.Body);
        foreach (var item in document.RootElement.GetProperty("resources").EnumerateArray())
        {
            var detail = await factory.Server.GetAsync(
                $"/v2/{ScimApplicationFactory.TestOrganizationId1}/{resource}/{item.GetProperty("id").GetString()}",
                context => context.Request.QueryString = new QueryString("?" + query));
            Assert.Equal(200, detail.Response.StatusCode);
            using var detailDocument = await JsonDocument.ParseAsync(detail.Response.Body);
            Assert.Equal(detailDocument.RootElement.GetProperty(membership).GetRawText(),
                item.GetProperty(membership).GetRawText());
        }
    }

    [Theory]
    [InlineData("Groups", "?excludedAttributes=members", false)]
    [InlineData("Users", "?attributes=userName", false)]
    [InlineData("Groups", "?startIndex=100", false)]
    [InlineData("Users", "?startIndex=100", false)]
    [InlineData("Groups", "?attributes=members.value", true)]
    [InlineData("Groups", "?excludedAttributes=members.display", true)]
    public async Task ListProjectionAndEmptyPages_AvoidUnnecessaryReads(
        string resource, string query, bool includesMemberships)
    {
        var reads = new ReadCounter();
        using var factory = CreateFactory(reads);
        reads.Commands.Clear();
        var response = await factory.Server.GetAsync(
            $"/v2/{ScimApplicationFactory.TestOrganizationId1}/{resource}",
            context => context.Request.QueryString = new QueryString(query));
        Assert.Equal(200, response.Response.StatusCode);
        var membershipReads = reads.Commands.Where(command => command.Contains("GroupUser", StringComparison.Ordinal));
        if (includesMemberships)
        {
            Assert.Single(membershipReads);
            Assert.InRange(reads.Commands.Count, 1, 4);
            Assert.DoesNotContain(reads.Commands, command => command.Contains("FROM \"User\"", StringComparison.Ordinal));
            using var document = await JsonDocument.ParseAsync(response.Response.Body);
            var group = document.RootElement.GetProperty("resources")[0];
            var members = group.GetProperty("members").EnumerateArray().ToArray();
            Assert.Equal(2, members.Length);
            Assert.All(members, member => Assert.False(member.TryGetProperty("display", out _)));
        }
        else
        {
            Assert.Empty(membershipReads);
        }
    }

    [Theory]
    [InlineData("Groups", "members", "")]
    [InlineData("Groups", "members", "?attributes=members.value")]
    [InlineData("Users", "groups", "")]
    public async Task Lists_ExcludeCrossOrganizationMemberships(string resource, string membership, string query)
    {
        using var factory = CreateFactory(new ReadCounter());
        var db = factory.GetDatabaseContext();
        var foreignOrganization = Guid.NewGuid();
        var foreignGroup = Guid.NewGuid();
        var foreignUser = Guid.NewGuid();
        db.Organizations.Add(new Organization
        {
            Id = foreignOrganization,
            Name = "Other organization",
            BillingEmail = "billing@example.invalid",
            Plan = "Enterprise"
        });
        db.Groups.Add(new Group { Id = foreignGroup, OrganizationId = foreignOrganization, Name = "Other group" });
        db.OrganizationUsers.Add(new OrganizationUser
        {
            Id = foreignUser,
            OrganizationId = foreignOrganization,
            Email = "other@example.invalid"
        });
        db.GroupUsers.AddRange(
            new GroupUser { GroupId = ScimApplicationFactory.TestGroupId1, OrganizationUserId = foreignUser },
            new GroupUser { GroupId = foreignGroup, OrganizationUserId = ScimApplicationFactory.TestOrganizationUserId1 });
        db.SaveChanges();

        var response = await factory.Server.GetAsync(
            $"/v2/{ScimApplicationFactory.TestOrganizationId1}/{resource}",
            context => context.Request.QueryString = new QueryString(query));
        Assert.Equal(200, response.Response.StatusCode);
        using var document = await JsonDocument.ParseAsync(response.Response.Body);
        var references = document.RootElement.GetProperty("resources").EnumerateArray()
            .SelectMany(item => item.GetProperty(membership).EnumerateArray()).ToArray();
        Assert.NotEmpty(references);
        Assert.DoesNotContain(references, reference =>
            reference.GetProperty("value").GetGuid() == (resource == "Groups" ? foreignUser : foreignGroup));
    }

    private static ScimApplicationFactory CreateFactory(ReadCounter reads)
    {
        var factory = new ScimApplicationFactory();
        factory.ConfigureServices(services =>
        {
            var options = services.Single(service => service.ServiceType == typeof(DbContextOptions<DatabaseContext>));
            services.Remove(options);
            services.AddScoped(provider => new DbContextOptionsBuilder<DatabaseContext>(
                    (DbContextOptions<DatabaseContext>)options.ImplementationFactory!(provider))
                .AddInterceptors(reads).Options);
        });
        factory.ReinitializeDbForTests(factory.GetDatabaseContext());
        return factory;
    }

    private sealed class ReadCounter : DbCommandInterceptor
    {
        public ConcurrentQueue<string> Commands { get; } = new();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Enqueue(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
