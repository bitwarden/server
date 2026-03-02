using Bit.Core.Enums;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Loads a roster fixture and creates users, groups, and collections with permissions.
/// </summary>
internal sealed class CreateRosterStep(string fixtureName) : IStep
{
    public void Execute(SeederContext context)
    {
        var org = context.RequireOrganization();
        var orgKey = context.RequireOrgKey();
        var orgId = context.RequireOrgId();
        var domain = context.RequireDomain();
        var roster = context.GetSeedReader().Read<SeedRoster>($"rosters.{fixtureName}");

        // Phase 1: Create users — build emailPrefix → orgUserId lookup
        var userLookup = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var emailPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rosterUser in roster.Users)
        {
            var emailPrefix = $"{rosterUser.FirstName}.{rosterUser.LastName}".ToLowerInvariant();

            if (!emailPrefixes.Add(emailPrefix))
            {
                throw new InvalidOperationException(
                    $"Duplicate email prefix '{emailPrefix}' in roster '{fixtureName}'. " +
                    "Each user must have a unique FirstName.LastName combination.");
            }


            var email = $"{emailPrefix}@{domain}";
            var mangledEmail = context.GetMangler().Mangle(email);
            var password = context.GetPassword();
            var userKeys = RustSdkService.GenerateUserKeys(mangledEmail, password);
            var (user, _) = UserSeeder.Create(mangledEmail, context.GetPasswordHasher(), context.GetMangler(), keys: userKeys, password: password);
            var userOrgKey = RustSdkService.GenerateUserOrganizationKey(user.PublicKey!, orgKey);
            var orgUserType = ParseRole(rosterUser.Role);
            var orgUser = org.CreateOrganizationUserWithKey(
                user, orgUserType, OrganizationUserStatusType.Confirmed, userOrgKey);

            // Promote the first owner-role user to pipeline owner
            if (orgUserType == OrganizationUserType.Owner && context.Owner is null)
            {
                context.Owner = user;
                context.OwnerOrgUser = orgUser;
            }

            userLookup[emailPrefix] = orgUser.Id;

            context.Users.Add(user);
            context.OrganizationUsers.Add(orgUser);
            context.Registry.HardenedOrgUserIds.Add(orgUser.Id);
            context.Registry.UserDigests.Add(
                new EntityRegistry.UserDigest(user.Id, orgUser.Id, userKeys.Key));
        }

        // Phase 2: Create groups — build groupName → groupId lookup
        var groupLookup = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        if (roster.Groups is not null)
        {
            foreach (var rosterGroup in roster.Groups)
            {
                var group = GroupSeeder.Create(orgId, rosterGroup.Name);
                groupLookup[rosterGroup.Name] = group.Id;
                context.Groups.Add(group);
                context.Registry.GroupIds.Add(group.Id);

                foreach (var memberPrefix in rosterGroup.Members)
                {
                    var orgUserId = RequireLookup(userLookup, memberPrefix,
                        $"Group '{rosterGroup.Name}' references unknown member '{memberPrefix}'.");
                    context.GroupUsers.Add(GroupUserSeeder.Create(group.Id, orgUserId));
                }
            }
        }

        // Phase 3: Create collections with group/user permission assignments
        if (roster.Collections is null)
        {
            return;
        }

        foreach (var rosterCollection in roster.Collections)
        {
            var collection = CollectionSeeder.Create(orgId, orgKey, rosterCollection.Name);
            context.Collections.Add(collection);
            context.Registry.CollectionIds.Add(collection.Id);

            if (rosterCollection.Groups is not null)
            {
                foreach (var cg in rosterCollection.Groups)
                {
                    var groupId = RequireLookup(groupLookup, cg.Group,
                        $"Collection '{rosterCollection.Name}' references unknown group '{cg.Group}'.");
                    context.CollectionGroups.Add(
                        CollectionGroupSeeder.Create(collection.Id, groupId, cg.ReadOnly, cg.HidePasswords, cg.Manage));
                }
            }

            if (rosterCollection.Users is null)
            {
                continue;
            }

            foreach (var cu in rosterCollection.Users)
            {
                var orgUserId = RequireLookup(userLookup, cu.User,
                    $"Collection '{rosterCollection.Name}' references unknown user '{cu.User}'.");
                context.CollectionUsers.Add(
                    CollectionUserSeeder.Create(collection.Id, orgUserId, cu.ReadOnly, cu.HidePasswords, cu.Manage));
            }
        }
    }

    private static Guid RequireLookup(Dictionary<string, Guid> lookup, string key, string errorMessage) =>
        lookup.TryGetValue(key, out var value)
            ? value
            : throw new InvalidOperationException(errorMessage);

    private static OrganizationUserType ParseRole(string role) =>
        role.ToLowerInvariant() switch
        {
            "owner" => OrganizationUserType.Owner,
            "admin" => OrganizationUserType.Admin,
            "user" => OrganizationUserType.User,
            "custom" => OrganizationUserType.Custom,
            _ => throw new InvalidOperationException(
                $"Unknown role '{role}'. Valid roles: owner, admin, user, custom.")
        };
}
