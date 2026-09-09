#nullable enable
using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

public class CollectionPermissionsTests
{
    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public void CanCreate_WhenOwnerOrAdmin_ReturnsTrue(OrganizationUserType type, Guid orgId)
    {
        var organizationClaims = new CurrentContextOrganization { Id = orgId, Type = type };
        var organizationAbility = new OrganizationAbility { Id = orgId, LimitCollectionCreation = true };

        Assert.True(CollectionPermissions.CanCreate(organizationClaims, organizationAbility));
    }

    [Theory, BitAutoData]
    public void CanCreate_WhenCustomUserWithCreateNewCollectionsPermission_ReturnsTrue(Guid orgId)
    {
        var organizationClaims = new CurrentContextOrganization
        {
            Id = orgId,
            Type = OrganizationUserType.Custom,
            Permissions = new Permissions { CreateNewCollections = true }
        };
        var organizationAbility = new OrganizationAbility { Id = orgId, LimitCollectionCreation = true };

        Assert.True(CollectionPermissions.CanCreate(organizationClaims, organizationAbility));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public void CanCreate_WhenMemberAndLimitCollectionCreationDisabled_ReturnsTrue(OrganizationUserType type, Guid orgId)
    {
        var organizationClaims = new CurrentContextOrganization { Id = orgId, Type = type };
        var organizationAbility = new OrganizationAbility { Id = orgId, LimitCollectionCreation = false };

        Assert.True(CollectionPermissions.CanCreate(organizationClaims, organizationAbility));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public void CanCreate_WhenMemberAndLimitCollectionCreationEnabledWithoutPermission_ReturnsFalse(OrganizationUserType type, Guid orgId)
    {
        var organizationClaims = new CurrentContextOrganization { Id = orgId, Type = type, Permissions = new Permissions() };
        var organizationAbility = new OrganizationAbility { Id = orgId, LimitCollectionCreation = true };

        Assert.False(CollectionPermissions.CanCreate(organizationClaims, organizationAbility));
    }

    [Fact]
    public void CanCreate_WhenNotAMember_ReturnsFalse()
    {
        Assert.False(CollectionPermissions.CanCreate(null, null));
    }

    [Theory, BitAutoData]
    public void CanCreate_WhenMemberAndNoOrganizationAbility_ReturnsTrue(Guid orgId)
    {
        // A null OrganizationAbility means LimitCollectionCreation is treated as disabled
        var organizationClaims = new CurrentContextOrganization { Id = orgId, Type = OrganizationUserType.User };

        Assert.True(CollectionPermissions.CanCreate(organizationClaims, null));
    }
}
