using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Identity.IdentityServer;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using NSubstitute;
using Xunit;
using Grant = Bit.Core.Auth.Entities.Grant;

namespace Bit.Identity.Test.IdentityServer;

public class PersistedGrantStoreTests
{
    private readonly IGrantRepository _grantRepository;
    private readonly PersistedGrantStore _sut;

    public PersistedGrantStoreTests()
    {
        _grantRepository = Substitute.For<IGrantRepository>();
        _sut = new PersistedGrantStore(_grantRepository, g => new Grant(g));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_GrantExists_ReturnsMappedPersistedGrant(Grant grant)
    {
        // Arrange
        _grantRepository.GetByKeyAsync(grant.Key).Returns(grant);

        // Act
        var result = await _sut.GetAsync(grant.Key);

        // Assert
        Assert.NotNull(result);
        AssertMapped(grant, result);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_GrantMissing_ReturnsNull(string key)
    {
        // Arrange
        _grantRepository.GetByKeyAsync(key).Returns((IGrant)null);

        // Act
        var result = await _sut.GetAsync(key);

        // Assert
        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task GetAllAsync_PassesFilterAndReturnsMappedGrants(
        PersistedGrantFilter filter, Grant firstGrant, Grant secondGrant)
    {
        // Arrange
        _grantRepository.GetManyAsync(filter.SubjectId, filter.SessionId, filter.ClientId, filter.Type)
            .Returns(new List<IGrant> { firstGrant, secondGrant });

        // Act
        var result = (await _sut.GetAllAsync(filter)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        AssertMapped(firstGrant, result[0]);
        AssertMapped(secondGrant, result[1]);
    }

    [Theory, BitAutoData]
    public async Task GetAllAsync_NoGrants_ReturnsEmpty(PersistedGrantFilter filter)
    {
        // Arrange
        _grantRepository.GetManyAsync(filter.SubjectId, filter.SessionId, filter.ClientId, filter.Type)
            .Returns(new List<IGrant>());

        // Act
        var result = await _sut.GetAllAsync(filter);

        // Assert
        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task RemoveAllAsync_DeletesGrantsMatchingFilter(PersistedGrantFilter filter)
    {
        // Act
        await _sut.RemoveAllAsync(filter);

        // Assert
        await _grantRepository.Received(1)
            .DeleteManyAsync(filter.SubjectId, filter.SessionId, filter.ClientId, filter.Type);
    }

    [Theory, BitAutoData]
    public async Task RemoveAsync_DeletesGrantByKey(string key)
    {
        // Act
        await _sut.RemoveAsync(key);

        // Assert
        await _grantRepository.Received(1).DeleteByKeyAsync(key);
    }

    [Theory, BitAutoData]
    public async Task StoreAsync_SavesConvertedGrant(PersistedGrant persistedGrant)
    {
        // Arrange
        IGrant savedGrant = null;
        await _grantRepository.SaveAsync(Arg.Do<IGrant>(g => savedGrant = g));

        // Act
        await _sut.StoreAsync(persistedGrant);

        // Assert
        Assert.NotNull(savedGrant);
        AssertMapped(savedGrant, persistedGrant);
    }

    [Theory, BitAutoData]
    public async Task StoreAsync_UsesProvidedGrantConverter(PersistedGrant persistedGrant, Grant convertedGrant)
    {
        // Arrange
        var sut = new PersistedGrantStore(_grantRepository, _ => convertedGrant);

        // Act
        await sut.StoreAsync(persistedGrant);

        // Assert
        await _grantRepository.Received(1).SaveAsync(convertedGrant);
    }

    private static void AssertMapped(IGrant grant, PersistedGrant persistedGrant)
    {
        Assert.Equal(grant.Key, persistedGrant.Key);
        Assert.Equal(grant.Type, persistedGrant.Type);
        Assert.Equal(grant.SubjectId, persistedGrant.SubjectId);
        Assert.Equal(grant.SessionId, persistedGrant.SessionId);
        Assert.Equal(grant.ClientId, persistedGrant.ClientId);
        Assert.Equal(grant.Description, persistedGrant.Description);
        Assert.Equal(grant.CreationDate, persistedGrant.CreationTime);
        Assert.Equal(grant.ExpirationDate, persistedGrant.Expiration);
        Assert.Equal(grant.ConsumedDate, persistedGrant.ConsumedTime);
        Assert.Equal(grant.Data, persistedGrant.Data);
    }
}
