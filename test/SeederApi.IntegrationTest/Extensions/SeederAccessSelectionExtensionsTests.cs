using Bit.Core.Models.Data;
using Bit.Seeder.Extensions;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Extensions;

public class SeederAccessSelectionExtensionsTests
{
    [Fact]
    public void DistinctOrThrowIfConflicting_Null_ReturnsNull()
    {
        IEnumerable<CollectionAccessSelection>? selections = null;

        Assert.Null(selections.DistinctOrThrowIfConflicting("group"));
    }

    [Fact]
    public void DistinctOrThrowIfConflicting_Empty_ReturnsEmpty()
    {
        var result = Array.Empty<CollectionAccessSelection>().DistinctOrThrowIfConflicting("group");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void DistinctOrThrowIfConflicting_IdenticalDuplicates_CollapsesToOne()
    {
        var id = Guid.NewGuid();

        var result = new[] { Selection(id, readOnly: true), Selection(id, readOnly: true) }
            .DistinctOrThrowIfConflicting("group");

        var selection = Assert.Single(result!);
        Assert.Equal(id, selection.Id);
        Assert.True(selection.ReadOnly);
    }

    [Fact]
    public void DistinctOrThrowIfConflicting_DistinctIds_KeepsAllInOrder()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var result = new[] { Selection(first), Selection(second, manage: true) }
            .DistinctOrThrowIfConflicting("group");

        Assert.Equal(new[] { first, second }, result!.Select(s => s.Id));
    }

    public static TheoryData<CollectionAccessSelection> ConflictingVariants() => new()
    {
        Selection(Guid.Empty, readOnly: true),
        Selection(Guid.Empty, hidePasswords: true),
        Selection(Guid.Empty, manage: true)
    };

    [Theory]
    [MemberData(nameof(ConflictingVariants))]
    public void DistinctOrThrowIfConflicting_SameIdWithDifferentFlag_ThrowsWithSubjectAndId(
        CollectionAccessSelection variant)
    {
        var id = Guid.NewGuid();
        variant.Id = id;

        var exception = Assert.Throws<InvalidOperationException>(
            () => new[] { Selection(id), variant }.DistinctOrThrowIfConflicting("organization user"));

        Assert.Equal($"Conflicting access settings requested for organization user {id}.", exception.Message);
    }

    private static CollectionAccessSelection Selection(
        Guid id, bool readOnly = false, bool hidePasswords = false, bool manage = false) =>
        new() { Id = id, ReadOnly = readOnly, HidePasswords = hidePasswords, Manage = manage };
}
