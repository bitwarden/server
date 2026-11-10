using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.Auth.Helpers;

/// <summary>
/// Assert helpers for SSO just-in-time (JIT) provisioning tests.
/// </summary>
internal static class SsoJitProvisionAssertions
{
    /// <summary>
    /// Asserts that the user has an organization user record in the organization with the expected status, and
    /// returns that record.
    /// </summary>
    public static async Task<OrganizationUser> AssertOrganizationUserStatusAsync(
        IOrganizationUserRepository organizationUserRepository, Guid organizationId, Guid userId,
        OrganizationUserStatusType expectedStatus)
    {
        var organizationUser = await organizationUserRepository.GetByOrganizationAsync(organizationId, userId);
        Assert.NotNull(organizationUser);
        Assert.Equal(expectedStatus, organizationUser.Status);
        return organizationUser;
    }
}
