using Bit.Core.Enums;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Creates the owner user and links them to the current organization.
/// </summary>
internal sealed class CreateOwnerStep : IStep
{
    public void Execute(SeederContext context)
    {
        var org = context.RequireOrganization();
        var password = context.Password;
        var kdfIterations = context.KdfIterations;
        var emailOverride = context.OwnerEmailOverride;
        var ownerEmail = !string.IsNullOrWhiteSpace(emailOverride)
            ? context.Mangler.Mangle(emailOverride)
            : context.Mangler.Mangle($"owner@{context.RequireDomain()}");
        var userKeys = RustSdkService.GenerateUserKeys(ownerEmail, password, kdfIterations, poolIndex: 0);
        var (owner, _) = UserSeeder.Create(
            new UserSeed
            {
                Email = ownerEmail,
                Keys = userKeys,
                Password = password,
                KdfIterations = kdfIterations
            },
            context.PasswordHasher,
            context.Mangler);

        var ownerOrgKey = RustSdkService.GenerateUserOrganizationKey(owner.PublicKey!, context.RequireOrgKey());
        var ownerOrgUser = org.CreateOrganizationUserWithKey(
            owner, OrganizationUserType.Owner, OrganizationUserStatusType.Confirmed, ownerOrgKey);

        context.Owner = owner;
        context.OwnerOrgUser = ownerOrgUser;

        context.Users.Add(owner);
        context.OrganizationUsers.Add(ownerOrgUser);
        context.Registry.HardenedOrgUserIds.Add(ownerOrgUser.Id);
        context.Registry.UserDigests.Add(new EntityRegistry.UserDigest(owner.Id, ownerOrgUser.Id, userKeys.Key));
    }
}
