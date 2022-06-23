using System;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Http;

namespace Bit.Scim.Context
{
    public interface IScimContext
    {
        HttpContext HttpContext { get; set; }
        ScimProviderType? ScimProvider { get; set; }
        Guid? OrganizationId { get; set; }
        Organization Organization { get; set; }
        Task BuildAsync(
            HttpContext httpContext,
            GlobalSettings globalSettings,
            IOrganizationRepository organizationRepository);
    }
}
