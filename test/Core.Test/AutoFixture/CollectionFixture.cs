using AutoFixture;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.AutoFixture;

public class CollectionCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        var orgId = Guid.NewGuid();
        var initialSeed = new Random().Next();

        // Use the same seed for each of these so that the collection Guids are the same for each type
        // This way when lists of various collection entities are created, they will have the same Guids
        var collectionIdRnd = new Random(initialSeed);
        var collectionDetailsIdRnd = new Random(initialSeed);
        var collectionUserCollectionIdRnd = new Random(initialSeed);

        // Use the same seed for each of these so that the user Guids are the same for each type
        // Increment the initial seed by 1 so that the user Guids are different from the collection Guids
        var userIdRnd = new Random(initialSeed + 1);
        var collectionUserUserIdRnd = new Random(initialSeed + 1);

        fixture.Customize<OrganizationUser>(composer => composer
            .With(o => o.OrganizationId, orgId)
            .With(o => o.Id, () => SeededGuid(userIdRnd)));

        fixture.Customize<Collection>(composer => composer
            .With(cu => cu.OrganizationId, orgId)
            .With(cu => cu.Id, () => SeededGuid(collectionIdRnd)));

        fixture.Customize<CollectionDetails>(composer => composer
            .With(cd => cd.OrganizationId, orgId)
            .With(cd => cd.Id, () => SeededGuid(collectionDetailsIdRnd)));

        fixture.Customize<CollectionUser>(c => c
            .With(cu => cu.OrganizationUserId, () => SeededGuid(collectionUserUserIdRnd))
            .With(cu => cu.CollectionId, () => SeededGuid(collectionUserCollectionIdRnd)));
    }

    private static Guid SeededGuid(Random rnd)
    {
        var bytes = new byte[16];
        rnd.NextBytes(bytes);
        return new Guid(bytes);
    }
}


public class CollectionCustomizationAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new CollectionCustomization();
}
