using Bit.Seeder.Extensions;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Extensions;

public class SeederEnumerableExtensionsTests
{
    [Fact]
    public void DistinctOrThrowIfConflicting_IdenticalDuplicates_KeepsFirstOccurrence()
    {
        var first = (Id: 1, Setting: "a", Tag: "first");
        var duplicate = (Id: 1, Setting: "a", Tag: "second");
        var other = (Id: 2, Setting: "b", Tag: "other");

        var result = new[] { first, duplicate, other }
            .DistinctOrThrowIfConflicting(i => i.Id, i => i.Setting, id => $"conflict {id}");

        Assert.Equal(new[] { first, other }, result);
    }
}
