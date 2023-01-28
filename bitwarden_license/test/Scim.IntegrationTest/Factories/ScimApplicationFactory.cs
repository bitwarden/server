using System.Net.Mime;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Bit.Core.Services;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.IntegrationTestCommon.Factories;
using Bit.Scim.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Bit.Scim.IntegrationTest.Factories;

public class ScimApplicationFactory : WebApplicationFactoryBase<Startup>
{
    public readonly new TestServer Server;

    public const string TestUserId1 = "2e8173db-8e8d-4de1-ac38-91b15c6d8dcb";
    public const string TestUserId2 = "b57846fc-0e94-4c93-9de5-9d0389eeadfb";
    public const string TestUserId3 = "20713eb8-d0c5-4655-b855-1a0f3472ccb5";
    public const string TestUserId4 = "cee613af-d0cb-4db9-ab9d-579bb120fd2a";
    public const string TestGroupId1 = "dcb232e8-761d-4152-a510-be2778d037cb";
    public const string TestGroupId2 = "562e5371-7020-40b6-b092-099ac66dbdf9";
    public const string TestGroupId3 = "362c2782-0f1f-4c86-95dd-edbdf7d6040b";
    public const string TestOrganizationId1 = "fb98e04f-0303-4914-9b37-a983943bf1ca";
    public const string TestOrganizationUserId1 = "5d421196-8c59-485b-8926-2d6d0101e05f";
    public const string TestOrganizationUserId2 = "3a63d520-0d84-4679-b887-13fe2058d53b";
    public const string TestOrganizationUserId3 = "be2f9045-e2b6-4173-ad44-4c69c3ea8140";
    public const string TestOrganizationUserId4 = "1f5689b7-e96e-4840-b0b1-eb3d5b5fd514";

    public ScimApplicationFactory()
    {
        WebApplicationFactory<Startup> webApplicationFactory = WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services
                    .AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });

                // Override to bypass SCIM authorization
                services.AddAuthorization(config =>
                {
                    config.AddPolicy("Scim", policy =>
                    {
                        policy.RequireAssertion(a => true);
                    });
                });

                var mailService = services.First(sd => sd.ServiceType == typeof(IMailService));
                services.Remove(mailService);
                services.AddSingleton<IMailService, NoopMailService>();
            });
        });

        Server = webApplicationFactory.Server;
    }

    public async Task<HttpContext> GroupsGetAsync(string organizationId, string id)
    {
        return await Server.GetAsync($"/v2/{organizationId}/groups/{id}");
    }

    public async Task<HttpContext> GroupsGetListAsync(string organizationId, string filter, int? count, int? startIndex)
    {
        var queryString = new QueryString("?");

        if (!string.IsNullOrWhiteSpace(filter))
        {
            queryString = queryString.Add("filter", filter);
        }

        if (count.HasValue)
        {
            queryString = queryString.Add("count", count.ToString());
        }

        if (startIndex.HasValue)
        {
            queryString = queryString.Add("startIndex", startIndex.ToString());
        }

        return await Server.GetAsync($"/v2/{organizationId}/groups", httpContext => httpContext.Request.QueryString = queryString);
    }

    public async Task<HttpContext> GroupsPostAsync(string organizationId, ScimGroupRequestModel model)
    {
        return await Server.PostAsync($"/v2/{organizationId}/groups", GetStringContent(model), httpContext => httpContext.Request.Headers.Add(HeaderNames.UserAgent, "Okta"));
    }

    public async Task<HttpContext> GroupsPutAsync(string organizationId, string id, ScimGroupRequestModel model)
    {
        return await Server.PutAsync($"/v2/{organizationId}/groups/{id}", GetStringContent(model), httpContext => httpContext.Request.Headers.Add(HeaderNames.UserAgent, "Okta"));
    }

    public async Task<HttpContext> GroupsPatchAsync(string organizationId, string id, ScimPatchModel model)
    {
        return await Server.PatchAsync($"/v2/{organizationId}/groups/{id}", GetStringContent(model));
    }

    public async Task<HttpContext> GroupsDeleteAsync(string organizationId, string id)
    {
        return await Server.DeleteAsync($"/v2/{organizationId}/groups/{id}", null);
    }

    public async Task<HttpContext> UsersGetAsync(string organizationId, string id)
    {
        return await Server.GetAsync($"/v2/{organizationId}/users/{id}");
    }

    public async Task<HttpContext> UsersGetListAsync(string organizationId, string filter, int? count, int? startIndex)
    {
        var queryString = new QueryString("?");

        if (!string.IsNullOrWhiteSpace(filter))
        {
            queryString = queryString.Add("filter", filter);
        }

        if (count.HasValue)
        {
            queryString = queryString.Add("count", count.ToString());
        }

        if (startIndex.HasValue)
        {
            queryString = queryString.Add("startIndex", startIndex.ToString());
        }

        return await Server.GetAsync($"/v2/{organizationId}/users", httpContext => httpContext.Request.QueryString = queryString);
    }

    public async Task<HttpContext> UsersPostAsync(string organizationId, ScimUserRequestModel model)
    {
        return await Server.PostAsync($"/v2/{organizationId}/users", GetStringContent(model));
    }

    public async Task<HttpContext> UsersPutAsync(string organizationId, string id, ScimUserRequestModel model)
    {
        return await Server.PutAsync($"/v2/{organizationId}/users/{id}", GetStringContent(model));
    }

    public async Task<HttpContext> UsersPatchAsync(string organizationId, string id, ScimPatchModel model)
    {
        return await Server.PatchAsync($"/v2/{organizationId}/users/{id}", GetStringContent(model));
    }

    public async Task<HttpContext> UsersDeleteAsync(string organizationId, string id, ScimUserRequestModel model)
    {
        return await Server.DeleteAsync($"/v2/{organizationId}/users/{id}", GetStringContent(model));
    }

    public void InitializeDbForTests(DatabaseContext databaseContext)
    {
        databaseContext.Organizations.AddRange(GetSeedingOrganizations());
        databaseContext.Groups.AddRange(GetSeedingGroups());
        databaseContext.Users.AddRange(GetSeedingUsers());
        databaseContext.OrganizationUsers.AddRange(GetSeedingOrganizationUsers());
        databaseContext.GroupUsers.AddRange(GetSeedingGroupUsers());
        databaseContext.SaveChanges();
    }

    public void ReinitializeDbForTests(DatabaseContext databaseContext)
    {
        databaseContext.Organizations.RemoveRange(databaseContext.Organizations);
        databaseContext.Groups.RemoveRange(databaseContext.Groups);
        databaseContext.Users.RemoveRange(databaseContext.Users);
        databaseContext.OrganizationUsers.RemoveRange(databaseContext.OrganizationUsers);
        databaseContext.GroupUsers.RemoveRange(databaseContext.GroupUsers);
        databaseContext.SaveChanges();
        InitializeDbForTests(databaseContext);
    }

    private List<Infrastructure.EntityFramework.Models.User> GetSeedingUsers()
    {
        return new List<Infrastructure.EntityFramework.Models.User>()
        {
            new Infrastructure.EntityFramework.Models.User { Id = Guid.Parse(TestUserId1), Name = "Test User 1", ApiKey = "", Email = "user1@example.com", SecurityStamp = "" },
            new Infrastructure.EntityFramework.Models.User { Id = Guid.Parse(TestUserId2), Name = "Test User 2", ApiKey = "", Email = "user2@example.com", SecurityStamp = "" },
            new Infrastructure.EntityFramework.Models.User { Id = Guid.Parse(TestUserId3), Name = "Test User 3", ApiKey = "", Email = "user3@example.com", SecurityStamp = "" },
            new Infrastructure.EntityFramework.Models.User { Id = Guid.Parse(TestUserId4), Name = "Test User 4", ApiKey = "", Email = "user4@example.com", SecurityStamp = "" },
        };
    }

    private List<Infrastructure.EntityFramework.Models.Group> GetSeedingGroups()
    {
        return new List<Infrastructure.EntityFramework.Models.Group>()
        {
            new Infrastructure.EntityFramework.Models.Group { Id = Guid.Parse(TestGroupId1), OrganizationId = Guid.Parse(TestOrganizationId1), Name = "Test Group 1", ExternalId = "A" },
            new Infrastructure.EntityFramework.Models.Group { Id = Guid.Parse(TestGroupId2), OrganizationId = Guid.Parse(TestOrganizationId1), Name = "Test Group 2", ExternalId = "B" },
            new Infrastructure.EntityFramework.Models.Group { Id = Guid.Parse(TestGroupId3), OrganizationId = Guid.Parse(TestOrganizationId1), Name = "Test Group 3", ExternalId = "C" }
        };
    }

    private List<Infrastructure.EntityFramework.Models.Organization> GetSeedingOrganizations()
    {
        return new List<Infrastructure.EntityFramework.Models.Organization>()
        {
            new Infrastructure.EntityFramework.Models.Organization { Id = Guid.Parse(TestOrganizationId1), Name = "Test Organization 1", UseGroups = true }
        };
    }

    private List<Infrastructure.EntityFramework.Models.OrganizationUser> GetSeedingOrganizationUsers()
    {
        return new List<Infrastructure.EntityFramework.Models.OrganizationUser>()
        {
            new Infrastructure.EntityFramework.Models.OrganizationUser { Id = Guid.Parse(TestOrganizationUserId1), OrganizationId = Guid.Parse(TestOrganizationId1), UserId = Guid.Parse(TestUserId1), Status = Core.Enums.OrganizationUserStatusType.Confirmed, ExternalId = "UA", Email = "user1@example.com" },
            new Infrastructure.EntityFramework.Models.OrganizationUser { Id = Guid.Parse(TestOrganizationUserId2), OrganizationId = Guid.Parse(TestOrganizationId1), UserId = Guid.Parse(TestUserId2), Status = Core.Enums.OrganizationUserStatusType.Confirmed, ExternalId = "UB", Email = "user2@example.com" },
            new Infrastructure.EntityFramework.Models.OrganizationUser { Id = Guid.Parse(TestOrganizationUserId3), OrganizationId = Guid.Parse(TestOrganizationId1), UserId = Guid.Parse(TestUserId3), Status = Core.Enums.OrganizationUserStatusType.Revoked, ExternalId = "UC", Email = "user3@example.com" },
            new Infrastructure.EntityFramework.Models.OrganizationUser { Id = Guid.Parse(TestOrganizationUserId4), OrganizationId = Guid.Parse(TestOrganizationId1), UserId = Guid.Parse(TestUserId4), Status = Core.Enums.OrganizationUserStatusType.Confirmed, ExternalId = "UD", Email = "user4@example.com" },
        };
    }

    private List<Infrastructure.EntityFramework.Models.GroupUser> GetSeedingGroupUsers()
    {
        return new List<Infrastructure.EntityFramework.Models.GroupUser>()
        {
            new Infrastructure.EntityFramework.Models.GroupUser { GroupId = Guid.Parse(TestGroupId1), OrganizationUserId = Guid.Parse(TestOrganizationUserId1) },
            new Infrastructure.EntityFramework.Models.GroupUser { GroupId = Guid.Parse(TestGroupId1), OrganizationUserId = Guid.Parse(TestOrganizationUserId4) }
        };
    }

    private static StringContent GetStringContent(object obj) => new(JsonSerializer.Serialize(obj), Encoding.Default, MediaTypeNames.Application.Json);

    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "Test user"),
                new Claim("orgadmin", TestOrganizationId1)
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");

            var result = AuthenticateResult.Success(ticket);

            return Task.FromResult(result);
        }
    }
}
