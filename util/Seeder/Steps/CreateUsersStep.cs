using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.RustSDK;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Factories;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Creates member users and links them to the current organization.
/// When <c>realisticStatusMix</c> is enabled (and count >= 10), users receive a
/// realistic distribution of Confirmed/Invited/Accepted/Revoked statuses.
/// </summary>
internal sealed class CreateUsersStep(int count, bool realisticStatusMix = false) : IStep
{
    public void Execute(SeederContext context)
    {
        var org = context.RequireOrganization();
        var orgKey = context.RequireOrgKey();
        var domain = context.RequireDomain();

        var statusDistribution = realisticStatusMix && count >= 10
            ? UserStatusDistributions.Realistic
            : UserStatusDistributions.AllConfirmed;

        var users = new List<User>(count);
        var organizationUsers = new List<OrganizationUser>(count);
        var hardenedOrgUserIds = new List<Guid>();
        var userDigests = new List<EntityRegistry.UserDigest>();
        var password = context.GetPassword();

        for (var i = 0; i < count; i++)
        {
            var email = $"user{i}@{domain}";
            var mangledEmail = context.GetMangler().Mangle(email);
            var userKeys = RustSdkService.GenerateUserKeys(mangledEmail, password);
            var (user, _) = UserSeeder.Create(mangledEmail, context.GetPasswordHasher(), context.GetMangler(), keys: userKeys, password: password);

            var status = statusDistribution.Select(i, count);

            var memberOrgKey = StatusRequiresOrgKey(status)
                ? RustSdkService.GenerateUserOrganizationKey(user.PublicKey!, orgKey)
                : null;

            var orgUser = org.CreateOrganizationUserWithKey(
                user, OrganizationUserType.User, status, memberOrgKey);

            users.Add(user);
            organizationUsers.Add(orgUser);

            if (status == OrganizationUserStatusType.Confirmed)
            {
                hardenedOrgUserIds.Add(orgUser.Id);
                userDigests.Add(new EntityRegistry.UserDigest(user.Id, orgUser.Id, userKeys.Key));
            }
        }

        context.Users.AddRange(users);
        context.OrganizationUsers.AddRange(organizationUsers);
        context.Registry.HardenedOrgUserIds.AddRange(hardenedOrgUserIds);
        context.Registry.UserDigests.AddRange(userDigests);
    }

    private static bool StatusRequiresOrgKey(OrganizationUserStatusType status) =>
        status is OrganizationUserStatusType.Confirmed or OrganizationUserStatusType.Revoked;
}
