using Bit.Seeder.Guards;
using Bit.Seeder.Models;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Guards;

public sealed class FixedOrganizationIdGuardTests
{
    private static readonly Guid _fixedId = Guid.Parse("a1b2c3d4-0000-4000-8000-000000000001");

    [Fact]
    public void EnsureAvailable_NullOrganization_DoesNotInvokePredicate()
    {
        var predicateCalled = false;

        FixedOrganizationIdGuard.EnsureAvailable(null, id =>
        {
            predicateCalled = true;
            return true;
        });

        Assert.False(predicateCalled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureAvailable_NullOrWhitespaceId_DoesNotInvokePredicate(string? id)
    {
        var predicateCalled = false;

        FixedOrganizationIdGuard.EnsureAvailable(new SeedPresetOrganization { Id = id }, orgId =>
        {
            predicateCalled = true;
            return true;
        });

        Assert.False(predicateCalled);
    }

    [Fact]
    public void EnsureAvailable_NoCollision_DoesNotThrow()
    {
        FixedOrganizationIdGuard.EnsureAvailable(
            new SeedPresetOrganization { Id = _fixedId.ToString() },
            id => false);
    }

    [Fact]
    public void EnsureAvailable_Collision_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            FixedOrganizationIdGuard.EnsureAvailable(
                new SeedPresetOrganization { Id = _fixedId.ToString() },
                id => true));

        Assert.Contains(_fixedId.ToString(), ex.Message);
    }

    [Fact]
    public void EnsureAvailable_PassesParsedIdToPredicate()
    {
        Guid? capturedId = null;

        FixedOrganizationIdGuard.EnsureAvailable(
            new SeedPresetOrganization { Id = _fixedId.ToString() },
            id =>
            {
                capturedId = id;
                return false;
            });

        Assert.Equal(_fixedId, capturedId);
    }

    [Fact]
    public void ResolveFixedId_NullOrganization_ReturnsNull()
    {
        Assert.Null(FixedOrganizationIdGuard.ResolveFixedId(null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveFixedId_NullOrWhitespaceId_ReturnsNull(string? id)
    {
        Assert.Null(FixedOrganizationIdGuard.ResolveFixedId(new SeedPresetOrganization { Id = id }));
    }

    [Fact]
    public void ResolveFixedId_ValidGuidString_ReturnsParsedGuid()
    {
        var result = FixedOrganizationIdGuard.ResolveFixedId(new SeedPresetOrganization { Id = _fixedId.ToString() });

        Assert.Equal(_fixedId, result);
    }

    [Fact]
    public void ResolveFixedId_MalformedId_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() =>
            FixedOrganizationIdGuard.ResolveFixedId(new SeedPresetOrganization { Id = "not-a-guid" }));
    }
}
