using AutoFixture;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.AutoFixture;

public class CollectionCustomization : ICustomization
{
    private const int _collectionIdSeed = 1;
    private const int _userIdSeed = 2;
    private const int _groupIdSeed = 3;

    public void Customize(IFixture fixture)
    {
        var orgId = Guid.NewGuid();

        fixture.Customize<OrganizationUser>(composer => composer
            .With(o => o.OrganizationId, orgId)
            .WithGuidFromSeed(o => o.Id, _userIdSeed));

        fixture.Customize<Collection>(composer => composer
            .With(c => c.OrganizationId, orgId)
            .WithGuidFromSeed(c => c.Id, _collectionIdSeed));

        fixture.Customize<CollectionDetails>(composer => composer
            .With(cd => cd.OrganizationId, orgId)
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
