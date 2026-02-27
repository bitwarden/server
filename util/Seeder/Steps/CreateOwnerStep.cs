using Bit.Core.Enums;
using Bit.RustSDK;
using Bit.Seeder.Factories;
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
        var password = context.GetPassword();
        var ownerEmail = context.GetMangler().Mangle($"owner@{context.RequireDomain()}");
        var userKeys = RustSdkService.GenerateUserKeys(ownerEmail, password);
        var (owner, _) = UserSeeder.Create(ownerEmail, context.GetPasswordHasher(), context.GetMangler(), keys: userKeys, password: password);


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
