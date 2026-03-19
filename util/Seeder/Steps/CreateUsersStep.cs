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

        var password = context.GetPassword();
        var kdfIterations = context.GetKdfIterations();
        var mangler = context.GetMangler();
        var passwordHasher = context.GetPasswordHasher();

        // Pre-compute mangled emails and statuses (ManglerService is not thread-safe)
        var mangledEmails = new string[count];
        var statuses = new OrganizationUserStatusType[count];
        for (var i = 0; i < count; i++)
        {
            mangledEmails[i] = mangler.Mangle($"user{i}@{domain}");
            statuses[i] = statusDistribution.Select(i, count);
        }

        var results = new (User User, OrganizationUser OrgUser, UserKeys Keys, bool IsConfirmed)[count];

        Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
        {
            var userKeys = RustSdkService.GenerateUserKeys(mangledEmails[i], password, kdfIterations);
            var (user, _) = UserSeeder.Create(mangledEmails[i], passwordHasher, mangler, keys: userKeys, password: password, kdfIterations: kdfIterations);

            var memberOrgKey = StatusRequiresOrgKey(statuses[i])
                ? RustSdkService.GenerateUserOrganizationKey(user.PublicKey!, orgKey)
                : null;

            var orgUser = org.CreateOrganizationUserWithKey(
                user, OrganizationUserType.User, statuses[i], memberOrgKey);

            results[i] = (user, orgUser, userKeys, statuses[i] == OrganizationUserStatusType.Confirmed);
        });

        var users = new List<User>(count);
        var organizationUsers = new List<OrganizationUser>(count);
        var hardenedOrgUserIds = new List<Guid>(count);
        var userDigests = new List<EntityRegistry.UserDigest>(count);

        for (var i = 0; i < count; i++)
        {
            var r = results[i];
            users.Add(r.User);
            organizationUsers.Add(r.OrgUser);

            if (r.IsConfirmed)
            {
                hardenedOrgUserIds.Add(r.OrgUser.Id);
                userDigests.Add(new EntityRegistry.UserDigest(r.User.Id, r.OrgUser.Id, r.Keys.Key));
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
