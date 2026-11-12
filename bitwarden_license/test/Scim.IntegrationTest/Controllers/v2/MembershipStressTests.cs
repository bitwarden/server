using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.IntegrationTestCommon;
using Bit.Scim.IntegrationTest.Factories;
using Bit.Scim.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Bit.Scim.IntegrationTest.Controllers.v2;

public class MembershipStressTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ITestOutputHelper _output;

    public MembershipStressTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [MembershipStressFact]
    [Trait("Category", "Stress")]
    public async Task MembershipReads_ReturnCompleteResponsesUnderConcurrentLoad()
    {
        await using var factory = new ScimApplicationFactory();
        factory.TestDatabase.Dispose();
        factory.TestDatabase = new ConcurrentSqliteTestDatabase();
        factory.UpdateConfiguration("Logging:LogLevel:Default", "Warning");
        factory.UpdateConfiguration("Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command", "Warning");
        var organizationId = ScimApplicationFactory.TestOrganizationId1;
        var groupIds = new[]
        {
            ScimApplicationFactory.TestGroupId1,
            ScimApplicationFactory.TestGroupId2,
            ScimApplicationFactory.TestGroupId3,
            Guid.NewGuid()
        };
        var groupNames = new[] { "Test Group 1", "Test Group 2", "Test Group 3", "Stress Group 4" };
        var expectedUsers = new Dictionary<Guid, (string Email, bool Active)>();
        var expectedMembers = new HashSet<Guid>();

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            factory.ReinitializeDbForTests(database);
            database.Groups.Add(new Bit.Infrastructure.EntityFramework.Models.Group
            {
                Id = groupIds[3],
                OrganizationId = organizationId,
                Name = groupNames[3]
            });
            database.GroupUsers.RemoveRange(database.GroupUsers);
            foreach (var user in database.OrganizationUsers.ToArray())
            {
                var active = user.Status == OrganizationUserStatusType.Confirmed;
                expectedUsers.Add(user.Id, (user.Email, active));
                if (active)
                {
                    expectedMembers.Add(user.Id);
                }
            }
            for (var index = 0; index < 7_996; index++)
            {
                var userId = Guid.NewGuid();
                var organizationUserId = Guid.NewGuid();
                var email = $"stress-{index:D4}@example.com";
                var active = index < 1_397;
                database.Users.Add(new Bit.Infrastructure.EntityFramework.Models.User
                {
                    Id = userId,
                    Email = email,
                    Name = email,
                    ApiKey = string.Empty,
                    SecurityStamp = string.Empty
                });
                database.OrganizationUsers.Add(new Bit.Infrastructure.EntityFramework.Models.OrganizationUser
                {
                    Id = organizationUserId,
                    OrganizationId = organizationId,
                    UserId = userId,
                    Email = email,
                    Status = active ? OrganizationUserStatusType.Confirmed : OrganizationUserStatusType.Revoked
                });
                expectedUsers.Add(organizationUserId, (email, active));
                if (active)
                {
                    expectedMembers.Add(organizationUserId);
                }
            }
            foreach (var memberId in expectedMembers)
            {
                database.GroupUsers.AddRange(groupIds.Select(groupId =>
                    new Bit.Infrastructure.EntityFramework.Models.GroupUser
                    {
                        GroupId = groupId,
                        OrganizationUserId = memberId
                    }));
            }
            await database.SaveChangesAsync();
        }

        Assert.Equal(8_000, expectedUsers.Count);
        Assert.Equal(1_400, expectedMembers.Count);
        using var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        var prefix = $"/v2/{organizationId}";
        var endpoints = new[] { "Users", "User detail", "Groups", "Group detail" };
        var orderedUsers = expectedUsers.OrderBy(user => user.Value.Email, StringComparer.Ordinal).Select(user => user.Key).ToArray();

        async Task<(double Milliseconds, int Bytes)> ReadAsync(int endpoint, int page)
        {
            var path = endpoint switch
            {
                0 => $"{prefix}/Users?startIndex={page * 50 + 1}",
                1 => $"{prefix}/Users/{ScimApplicationFactory.TestOrganizationUserId1}",
                2 => $"{prefix}/Groups",
                _ => $"{prefix}/Groups/{groupIds[0]}"
            };
            var stopwatch = Stopwatch.StartNew();
            using var response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead);
            var body = await response.Content.ReadAsByteArrayAsync();
            stopwatch.Stop();
            Assert.True(response.StatusCode == HttpStatusCode.OK,
                $"{path}: HTTP {(int)response.StatusCode}: {System.Text.Encoding.UTF8.GetString(body)}");
            switch (endpoint)
            {
                case 0:
                    var users = JsonSerializer.Deserialize<ScimListResponseModel<ScimUserResponseModel>>(body, _jsonOptions);
                    Assert.NotNull(users);
                    Assert.Equal(8_000, users.TotalResults);
                    Assert.Equal(50, users.ItemsPerPage);
                    Assert.Equal(page * 50 + 1, users.StartIndex);
                    Assert.Equal(orderedUsers.Skip(page * 50).Take(50), users.Resources.Select(user => user.Id));
                    foreach (var user in users.Resources)
                    {
                        AssertUser(user);
                    }
                    break;
                case 1:
                    var detail = JsonSerializer.Deserialize<ScimUserResponseModel>(body, _jsonOptions);
                    Assert.NotNull(detail);
                    Assert.Equal(ScimApplicationFactory.TestOrganizationUserId1, detail.Id);
                    AssertUser(detail);
                    break;
                case 2:
                    var groups = JsonSerializer.Deserialize<ScimListResponseModel<ScimGroupResponseModel>>(body, _jsonOptions);
                    Assert.NotNull(groups);
                    Assert.Equal(4, groups.TotalResults);
                    Assert.Equal(50, groups.ItemsPerPage);
                    Assert.Equal(1, groups.StartIndex);
                    Assert.Equal(groupIds.Order(), groups.Resources.Select(group => group.Id).Order());
                    foreach (var group in groups.Resources)
                    {
                        AssertGroup(group);
                    }
                    break;
                default:
                    var groupDetail = JsonSerializer.Deserialize<ScimGroupResponseModel>(body, _jsonOptions);
                    Assert.NotNull(groupDetail);
                    Assert.Equal(groupIds[0], groupDetail.Id);
                    AssertGroup(groupDetail);
                    break;
            }
            return (stopwatch.Elapsed.TotalMilliseconds, body.Length);
        }

        void AssertUser(ScimUserResponseModel user)
        {
            Assert.Equal(expectedUsers[user.Id].Email, user.UserName);
            Assert.Equal(expectedUsers[user.Id].Active, user.Active);
            var expectedGroups = expectedMembers.Contains(user.Id) ? groupIds.Order().ToArray() : [];
            Assert.Equal(expectedGroups, user.Groups.Select(group => Guid.Parse(group.Value)));
            foreach (var group in user.Groups)
            {
                var id = Guid.Parse(group.Value);
                Assert.Equal(groupNames[Array.IndexOf(groupIds, id)], group.Display);
                Assert.Equal("direct", group.Type);
                Assert.Equal($"http://localhost{prefix}/Groups/{id}", group.Ref);
            }
        }

        void AssertGroup(ScimGroupResponseModel group)
        {
            Assert.Equal(groupNames[Array.IndexOf(groupIds, group.Id)], group.DisplayName);
            Assert.Equal(expectedMembers.Order(), group.Members.Select(member => Guid.Parse(member.Value)));
            foreach (var member in group.Members)
            {
                var id = Guid.Parse(member.Value);
                Assert.Equal(expectedUsers[id].Email, member.Display);
                Assert.Equal("User", member.Type);
                Assert.Equal($"http://localhost{prefix}/Users/{id}", member.Ref);
            }
        }

        for (var endpoint = 0; endpoint < endpoints.Length; endpoint++)
        {
            await ReadAsync(endpoint, 0);
        }

        foreach (var concurrency in new[] { 1, 5, 10 })
        {
            var measurements = new ConcurrentQueue<(int Endpoint, double Milliseconds, int Bytes)>();
            var run = Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(false);
            var allocatedBefore = GC.GetTotalAllocatedBytes();
            using var process = Process.GetCurrentProcess();
            process.Refresh();
            var peakWorkingSet = process.WorkingSet64;
            var peakManaged = memoryBefore;
            var sequence = -1;
            var workers = Enumerable.Range(0, concurrency).Select(async _ =>
            {
                while (run.Elapsed < TimeSpan.FromSeconds(30))
                {
                    var request = Interlocked.Increment(ref sequence);
                    var endpoint = request % endpoints.Length;
                    var result = await ReadAsync(endpoint, request / endpoints.Length % 160);
                    measurements.Enqueue((endpoint, result.Milliseconds, result.Bytes));
                }
            }).ToArray();
            var completion = Task.WhenAll(workers);
            while (!completion.IsCompleted)
            {
                process.Refresh();
                peakWorkingSet = Math.Max(peakWorkingSet, process.WorkingSet64);
                peakManaged = Math.Max(peakManaged, GC.GetTotalMemory(false));
                await Task.WhenAny(completion, Task.Delay(100));
            }
            await completion;
            run.Stop();
            _output.WriteLine(
                $"Concurrency {concurrency}: {measurements.Count} validated requests, zero errors, " +
                $"{measurements.Count / run.Elapsed.TotalSeconds:F2} requests/s over {run.Elapsed.TotalSeconds:F2}s; " +
                $"managed before/after/observed peak {memoryBefore}/{GC.GetTotalMemory(false)}/{peakManaged} bytes; " +
                $"observed process working-set peak {peakWorkingSet} bytes; " +
                $"process allocated {GC.GetTotalAllocatedBytes() - allocatedBefore} bytes.");
            for (var endpoint = 0; endpoint < endpoints.Length; endpoint++)
            {
                var samples = measurements.Where(sample => sample.Endpoint == endpoint).ToArray();
                Assert.NotEmpty(samples);
                var latency = samples.Select(sample => sample.Milliseconds).Order().ToArray();
                var p95 = latency[(int)Math.Ceiling(latency.Length * 0.95) - 1];
                _output.WriteLine(
                    $"{endpoints[endpoint]}: n={samples.Length}, p95 full-body latency={p95:F2}ms, " +
                    $"max={latency[^1]:F2}ms, mean payload={samples.Average(sample => sample.Bytes):F0} bytes.");
            }
        }
    }

    private sealed class MembershipStressFactAttribute : FactAttribute
    {
        public MembershipStressFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("SCIM_STRESS_TESTS") != "1")
            {
                Skip = "Opt-in local stress test: set SCIM_STRESS_TESTS=1.";
            }
        }
    }

    // The standard test database shares one connection, which cannot execute concurrent requests safely.
    private sealed class ConcurrentSqliteTestDatabase : ITestDatabase
    {
        private readonly string _connectionString = $"Data Source=scim-stress-{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        private readonly SqliteConnection _keepAlive;

        public ConcurrentSqliteTestDatabase()
        {
            _keepAlive = new SqliteConnection(_connectionString);
            _keepAlive.Open();
        }

        public void AddDatabase(IServiceCollection services)
        {
            services.AddScoped(provider => new DbContextOptionsBuilder<DatabaseContext>()
                .UseSqlite(_connectionString)
                .UseApplicationServiceProvider(provider)
                .Options);
        }

        public void Migrate(IServiceCollection services)
        {
            using var provider = new ServiceCollection()
                .AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider())
                .BuildServiceProvider();
            using var database = new DatabaseContext(new DbContextOptionsBuilder<DatabaseContext>()
                .UseSqlite(_connectionString).UseApplicationServiceProvider(provider).Options);
            database.Database.EnsureCreated();
        }

        public void Dispose() => _keepAlive.Dispose();
    }
}
