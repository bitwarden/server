using Bit.Api.Models.Request;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core.Models.Data;
using Xunit;

namespace Bit.Api.Test.Vault.AuthorizationHandlers;

public class CollectionAccessExtensionsTests
{
    [Fact]
    public void DiffCollectionAccess_AllNew_ReturnsAllAsCreates()
    {
        var posted = new[]
        {
            new SelectionReadOnlyRequestModel { Id = Guid.NewGuid() },
            new SelectionReadOnlyRequestModel { Id = Guid.NewGuid() }
        };
        var current = Array.Empty<CollectionAccessSelection>();

        var (createIds, updateIds, deleteIds) = posted.DiffCollectionAccess(current);

        Assert.Equal(2, createIds.Count);
        Assert.Empty(updateIds);
        Assert.Empty(deleteIds);
    }

    [Fact]
    public void DiffCollectionAccess_AllExisting_ReturnsAllAsUpdates()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var posted = new[]
        {
            new SelectionReadOnlyRequestModel { Id = id1 },
            new SelectionReadOnlyRequestModel { Id = id2 }
        };
        var current = new[]
        {
            new CollectionAccessSelection { Id = id1 },
            new CollectionAccessSelection { Id = id2 }
        };

        var (createIds, updateIds, deleteIds) = posted.DiffCollectionAccess(current);

        Assert.Empty(createIds);
        Assert.Equal(2, updateIds.Count);
        Assert.Empty(deleteIds);
    }

    [Fact]
    public void DiffCollectionAccess_RemovedItems_ReturnsAsDeletes()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var posted = new[]
        {
            new SelectionReadOnlyRequestModel { Id = id1 }
        };
        var current = new[]
        {
            new CollectionAccessSelection { Id = id1 },
            new CollectionAccessSelection { Id = id2 }
        };

        var (createIds, updateIds, deleteIds) = posted.DiffCollectionAccess(current);

        Assert.Empty(createIds);
        Assert.Single(updateIds);
        Assert.Single(deleteIds);
        Assert.Contains(id2, deleteIds);
    }

    [Fact]
    public void DiffCollectionAccess_MixedCreateUpdateDelete_CategorizesCorrectly()
    {
        var existingId = Guid.NewGuid();
        var removedId = Guid.NewGuid();
        var newId = Guid.NewGuid();

        var posted = new[]
        {
            new SelectionReadOnlyRequestModel { Id = existingId },
            new SelectionReadOnlyRequestModel { Id = newId }
        };
        var current = new[]
        {
            new CollectionAccessSelection { Id = existingId },
            new CollectionAccessSelection { Id = removedId }
        };

        var (createIds, updateIds, deleteIds) = posted.DiffCollectionAccess(current);

        Assert.Single(createIds);
        Assert.Contains(newId, createIds);
        Assert.Single(updateIds);
        Assert.Contains(existingId, updateIds);
        Assert.Single(deleteIds);
        Assert.Contains(removedId, deleteIds);
    }

    [Fact]
    public void DiffCollectionAccess_EmptyPosted_ReturnsAllAsDeletes()
    {
        var posted = Array.Empty<SelectionReadOnlyRequestModel>();
        var current = new[]
        {
            new CollectionAccessSelection { Id = Guid.NewGuid() },
            new CollectionAccessSelection { Id = Guid.NewGuid() }
        };

        var (createIds, updateIds, deleteIds) = posted.DiffCollectionAccess(current);

        Assert.Empty(createIds);
        Assert.Empty(updateIds);
        Assert.Equal(2, deleteIds.Count);
    }

    [Fact]
    public void DiffCollectionAccess_EmptyCurrent_ReturnsAllAsCreates()
    {
        var posted = new[]
        {
            new SelectionReadOnlyRequestModel { Id = Guid.NewGuid() },
            new SelectionReadOnlyRequestModel { Id = Guid.NewGuid() }
        };
        var current = Array.Empty<CollectionAccessSelection>();

        var (createIds, updateIds, deleteIds) = posted.DiffCollectionAccess(current);

        Assert.Equal(2, createIds.Count);
        Assert.Empty(updateIds);
        Assert.Empty(deleteIds);
    }

    [Fact]
    public void DiffCollectionAccess_BothEmpty_ReturnsAllEmpty()
    {
        var posted = Array.Empty<SelectionReadOnlyRequestModel>();
        var current = Array.Empty<CollectionAccessSelection>();

        var (createIds, updateIds, deleteIds) = posted.DiffCollectionAccess(current);

        Assert.Empty(createIds);
        Assert.Empty(updateIds);
        Assert.Empty(deleteIds);
    }
}
