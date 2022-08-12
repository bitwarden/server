using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Scim;
using Bit.Scim.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.IntegrationTestCommon.Factories
{
    public class ScimApplicationFactory : WebApplicationFactoryBase<Startup>
    {
        public readonly new TestServer Server;

        public readonly Guid TestUserId1 = Guid.NewGuid();
        public readonly Guid TestUserId2 = Guid.NewGuid();
        public readonly Guid TestUserId3 = Guid.NewGuid();
        public readonly Guid TestGroupId1 = Guid.NewGuid();
        public readonly Guid TestGroupId2 = Guid.NewGuid();
        public readonly Guid TestGroupId3 = Guid.NewGuid();
        public readonly Guid TestOrganizationId1 = Guid.NewGuid();
        public readonly Guid TestOrganizationUserId1 = Guid.NewGuid();
        public readonly Guid TestOrganizationUserId2 = Guid.NewGuid();
        public readonly Guid TestOrganizationUserId3 = Guid.NewGuid();

        public ScimApplicationFactory()
        {
            WebApplicationFactory<Startup> webApplicationFactory = WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Override to bypass SCIM authorization
                    services.AddAuthorization(config =>
                    {
                        config.AddPolicy("Scim", policy =>
                        {
                            policy.RequireAssertion(a => true);
                        });
                    });
                });
            });

            Server = webApplicationFactory.Server;
        }

        public async Task<HttpContext> GroupsGetAsync(Guid organizationId, Guid id)
        {
            return await Server.GetAsync($"/v2/{organizationId}/groups/{id}");
        }

        public async Task<HttpContext> GroupsGetListAsync(Guid organizationId, string filter, int? count, int? startIndex)
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

        public async Task<HttpContext> GroupsPostAsync(Guid organizationId, ScimGroupRequestModel model)
        {
            return await Server.PostAsync($"/v2/{organizationId}/groups", GetStringContent(model));
        }

        public async Task<HttpContext> GroupsPutAsync(Guid organizationId, Guid id, ScimGroupRequestModel model)
        {
            return await Server.PutAsync($"/v2/{organizationId}/groups/{id}", GetStringContent(model));
        }

        public async Task<HttpContext> GroupsPatchAsync(Guid organizationId, Guid id, ScimPatchModel model)
        {
            return await Server.PatchAsync($"/v2/{organizationId}/groups/{id}", GetStringContent(model));
        }

        public async Task<HttpContext> GroupsDeleteAsync(Guid organizationId, Guid id)
        {
            return await Server.DeleteAsync($"/v2/{organizationId}/groups/{id}");
        }

        public async Task<HttpContext> UsersGetAsync(Guid organizationId, Guid id)
        {
            return await Server.GetAsync($"/v2/{organizationId}/users/{id}");
        }

        public async Task<HttpContext> UsersGetListAsync(Guid organizationId, string filter, int? count, int? startIndex)
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

        public async Task<HttpContext> UsersPostAsync(Guid organizationId, ScimUserRequestModel model)
        {
            return await Server.PostAsync($"/v2/{organizationId}/users", GetStringContent(model));
        }

        public async Task<HttpContext> UsersPutAsync(Guid organizationId, Guid id, ScimUserRequestModel model)
        {
            return await Server.PutAsync($"/v2/{organizationId}/users/{id}", GetStringContent(model));
        }

        public async Task<HttpContext> UsersPatchAsync(Guid organizationId, Guid id, ScimPatchModel model)
        {
            return await Server.PatchAsync($"/v2/{organizationId}/users/{id}", GetStringContent(model));
        }

        public async Task<HttpContext> UsersDeleteAsync(Guid organizationId, Guid id)
        {
            return await Server.DeleteAsync($"/v2/{organizationId}/users/{id}");
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
                new Infrastructure.EntityFramework.Models.User { Id = TestUserId1, Name = "Test User 1", ApiKey = "", Email = "", SecurityStamp = "" },
                new Infrastructure.EntityFramework.Models.User { Id = TestUserId2, Name = "Test User 2", ApiKey = "", Email = "", SecurityStamp = "" },
                new Infrastructure.EntityFramework.Models.User { Id = TestUserId3, Name = "Test User 3", ApiKey = "", Email = "", SecurityStamp = "" }
            };
        }

        private List<Infrastructure.EntityFramework.Models.Group> GetSeedingGroups()
        {
            return new List<Infrastructure.EntityFramework.Models.Group>()
            {
                new Infrastructure.EntityFramework.Models.Group { Id = TestGroupId1, OrganizationId = TestOrganizationId1, Name = "Test Group 1", ExternalId = "A" },
                new Infrastructure.EntityFramework.Models.Group { Id = TestGroupId2, OrganizationId = TestOrganizationId1, Name = "Test Group 2", ExternalId = "B" },
                new Infrastructure.EntityFramework.Models.Group { Id = TestGroupId3, OrganizationId = TestOrganizationId1, Name = "Test Group 3", ExternalId = "C" }
            };
        }

        private List<Infrastructure.EntityFramework.Models.Organization> GetSeedingOrganizations()
        {
            return new List<Infrastructure.EntityFramework.Models.Organization>()
            {
                new Infrastructure.EntityFramework.Models.Organization { Id = TestOrganizationId1, Name = "Test Organization 1", UseGroups = true }
            };
        }

        private List<Infrastructure.EntityFramework.Models.OrganizationUser> GetSeedingOrganizationUsers()
        {
            return new List<Infrastructure.EntityFramework.Models.OrganizationUser>()
            {
                new Infrastructure.EntityFramework.Models.OrganizationUser { Id = TestOrganizationUserId1, OrganizationId = TestOrganizationId1, UserId = TestUserId1, Status = Core.Enums.OrganizationUserStatusType.Confirmed },
                new Infrastructure.EntityFramework.Models.OrganizationUser { Id = TestOrganizationUserId2, OrganizationId = TestOrganizationId1, UserId = TestUserId2, Status = Core.Enums.OrganizationUserStatusType.Confirmed },
                new Infrastructure.EntityFramework.Models.OrganizationUser { Id = TestOrganizationUserId3, OrganizationId = TestOrganizationId1, UserId = TestUserId3, Status = Core.Enums.OrganizationUserStatusType.Confirmed }
            };
        }

        private List<Infrastructure.EntityFramework.Models.GroupUser> GetSeedingGroupUsers()
        {
            return new List<Infrastructure.EntityFramework.Models.GroupUser>()
            {
                new Infrastructure.EntityFramework.Models.GroupUser { GroupId = TestGroupId1, OrganizationUserId = TestOrganizationUserId1 }
            };
        }

        private static StringContent GetStringContent(object obj) => new(JsonSerializer.Serialize(obj), Encoding.Default, MediaTypeNames.Application.Json);
    }
}
