using AutoFixture;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Test.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.Vault.AutoFixture;

public class CollectionCustomization : ICustomization
{
    private const int _collectionIdSeed = 1;
    private const int _userIdSeed = 2;
    private const int _groupIdSeed = 3;

    public void Customize(IFixture fixture)
    {
        var orgId = Guid.NewGuid();

        fixture.Customize<Organization>(composer => composer
            .With(o => o.Id, orgId));

        fixture.Customize<CurrentContextOrganization>(composer => composer
            .With(o => o.Id, orgId));

        fixture.Customize<OrganizationUser>(composer => composer
            .With(o => o.OrganizationId, orgId)
            .WithGuidFromSeed(o => o.Id, _userIdSeed));

        fixture.Customize<Collection>(composer => composer
            .With(o => o.OrganizationId, orgId)
            .WithGuidFromSeed(c => c.Id, _collectionIdSeed));

        fixture.Customize<CollectionDetails>(composer => composer
            .With(o => o.OrganizationId, orgId)
            .WithGuidFromSeed(cd => cd.Id, _collectionIdSeed));

        fixture.Customize<CollectionUser>(c => c
            .WithGuidFromSeed(cu => cu.OrganizationUserId, _userIdSeed)
            .WithGuidFromSeed(cu => cu.CollectionId, _collectionIdSeed));

        fixture.Customize<Group>(composer => composer
            .With(o => o.OrganizationId, orgId)
            .WithGuidFromSeed(o => o.Id, _groupIdSeed));

        fixture.Customize<CollectionGroup>(c => c
            .WithGuidFromSeed(cu => cu.GroupId, _groupIdSeed)
            .WithGuidFromSeed(cu => cu.CollectionId, _collectionIdSeed));
    }
}

public class CollectionCustomizationAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new CollectionCustomization();
}
