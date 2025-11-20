using AutoFixture;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.ReportFeatures;

[SutProviderCustomize]
public class MemberAccessReportQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetMemberAccessReportsAsync_MapsAvatarColor_FromDatabaseToModel(
        SutProvider<MemberAccessReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        var request = new MemberAccessReportRequest { OrganizationId = organizationId };
        var expectedAvatarColor = "#FF5733";

        var baseDetails = new List<OrganizationMemberBaseDetail>
        {
            new OrganizationMemberBaseDetail
            {
                UserGuid = Guid.NewGuid(),
                UserName = "Test User",
                Email = "test@example.com",
                AvatarColor = expectedAvatarColor,
                TwoFactorProviders = "[]",
                UsesKeyConnector = false,
                ResetPasswordKey = null,
                CollectionId = Guid.NewGuid(),
                GroupId = null,
                GroupName = null,
                CollectionName = "Test Collection",
                ReadOnly = false,
                HidePasswords = false,
                Manage = true,
                CipherId = Guid.NewGuid()
            }
        };

        sutProvider.GetDependency<IOrganizationMemberBaseDetailRepository>()
            .GetOrganizationMemberBaseDetailsByOrganizationId(organizationId)
            .Returns(baseDetails);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>
            {
                (baseDetails[0].UserGuid.Value, false)
            });

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { UseResetPassword = false });

        // Act
        var result = await sutProvider.Sut.GetMemberAccessReportsAsync(request);

        // Assert
        var resultList = result.ToList();
        Assert.Single(resultList);
        Assert.Equal(expectedAvatarColor, resultList[0].AvatarColor);
    }

    [Theory]
    [BitAutoData]
    public async Task GetMemberAccessReportsAsync_ExcludesUsersWithGroupBasedAccess_FromNoAccessSection(
        SutProvider<MemberAccessReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        var request = new MemberAccessReportRequest { OrganizationId = organizationId };
        var userWithGroupAccess = Guid.NewGuid();
        var userWithDirectAccess = Guid.NewGuid();
        var userWithNoAccess = Guid.NewGuid();

        var baseDetails = new List<OrganizationMemberBaseDetail>
        {
            // User with group-based collection access
            new OrganizationMemberBaseDetail
            {
                UserGuid = userWithGroupAccess,
                UserName = "Group User",
                Email = "group@example.com",
                AvatarColor = "#FF0000",
                TwoFactorProviders = "[]",
                UsesKeyConnector = false,
                ResetPasswordKey = null,
                CollectionId = Guid.NewGuid(),
                GroupId = Guid.NewGuid(),
                GroupName = "Test Group",
                CollectionName = "Test Collection",
                ReadOnly = false,
                HidePasswords = false,
                Manage = true,
                CipherId = Guid.NewGuid()
            },
            // User with direct collection access
            new OrganizationMemberBaseDetail
            {
                UserGuid = userWithDirectAccess,
                UserName = "Direct User",
                Email = "direct@example.com",
                AvatarColor = "#00FF00",
                TwoFactorProviders = "[]",
                UsesKeyConnector = false,
                ResetPasswordKey = null,
                CollectionId = Guid.NewGuid(),
                GroupId = null,
                GroupName = null,
                CollectionName = "Another Collection",
                ReadOnly = false,
                HidePasswords = false,
                Manage = true,
                CipherId = Guid.NewGuid()
            }
        };

        sutProvider.GetDependency<IOrganizationMemberBaseDetailRepository>()
            .GetOrganizationMemberBaseDetailsByOrganizationId(organizationId)
            .Returns(baseDetails);

        var userGuids = new[] { userWithGroupAccess, userWithDirectAccess };
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(userGuids.Select(id => (id, false)).ToList());

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { UseResetPassword = false });

        // Act
        var result = await sutProvider.Sut.GetMemberAccessReportsAsync(request);

        // Assert
        var resultList = result.ToList();
        Assert.Equal(2, resultList.Count);

        // Verify both users with access are in results
        Assert.Contains(resultList, r => r.UserGuid == userWithGroupAccess);
        Assert.Contains(resultList, r => r.UserGuid == userWithDirectAccess);

        // Verify the user with group access has group information
        var groupUser = resultList.First(r => r.UserGuid == userWithGroupAccess);
        Assert.NotNull(groupUser.GroupId);
        Assert.Equal("Test Group", groupUser.GroupName);
    }

    [Theory]
    [BitAutoData]
    public async Task GetMemberAccessReportsAsync_HandlesNullCipherIds_CorrectlyFiltersToNonNullable(
        SutProvider<MemberAccessReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        var request = new MemberAccessReportRequest { OrganizationId = organizationId };
        var userGuid = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        var cipherId1 = Guid.NewGuid();
        var cipherId2 = Guid.NewGuid();

        var baseDetails = new List<OrganizationMemberBaseDetail>
        {
            // Same user, same collection, multiple ciphers (some null)
            new OrganizationMemberBaseDetail
            {
                UserGuid = userGuid,
                UserName = "Test User",
                Email = "test@example.com",
                AvatarColor = "#FF5733",
                TwoFactorProviders = "[]",
                UsesKeyConnector = false,
                ResetPasswordKey = null,
                CollectionId = collectionId,
                GroupId = null,
                GroupName = null,
                CollectionName = "Test Collection",
                ReadOnly = false,
                HidePasswords = false,
                Manage = true,
                CipherId = cipherId1
            },
            new OrganizationMemberBaseDetail
            {
                UserGuid = userGuid,
                UserName = "Test User",
                Email = "test@example.com",
                AvatarColor = "#FF5733",
                TwoFactorProviders = "[]",
                UsesKeyConnector = false,
                ResetPasswordKey = null,
                CollectionId = collectionId,
                GroupId = null,
                GroupName = null,
                CollectionName = "Test Collection",
                ReadOnly = false,
                HidePasswords = false,
                Manage = true,
                CipherId = null // Null cipher
            },
            new OrganizationMemberBaseDetail
            {
                UserGuid = userGuid,
                UserName = "Test User",
                Email = "test@example.com",
                AvatarColor = "#FF5733",
                TwoFactorProviders = "[]",
                UsesKeyConnector = false,
                ResetPasswordKey = null,
                CollectionId = collectionId,
                GroupId = null,
                GroupName = null,
                CollectionName = "Test Collection",
                ReadOnly = false,
                HidePasswords = false,
                Manage = true,
                CipherId = cipherId2
            }
        };

        sutProvider.GetDependency<IOrganizationMemberBaseDetailRepository>()
            .GetOrganizationMemberBaseDetailsByOrganizationId(organizationId)
            .Returns(baseDetails);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)> { (userGuid, false) });

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { UseResetPassword = false });

        // Act
        var result = await sutProvider.Sut.GetMemberAccessReportsAsync(request);

        // Assert
        var resultList = result.ToList();
        Assert.Single(resultList);

        var cipherIds = resultList[0].CipherIds.ToList();

        // Should only contain non-null cipher IDs
        Assert.Equal(2, cipherIds.Count);
        Assert.Contains(cipherId1, cipherIds);
        Assert.Contains(cipherId2, cipherIds);
        Assert.DoesNotContain(Guid.Empty, cipherIds);
    }

    [Theory]
    [BitAutoData]
    public async Task GetMemberAccessReportsAsync_GroupsByUserAndCollection_WithCorrectPermissions(
        SutProvider<MemberAccessReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        var request = new MemberAccessReportRequest { OrganizationId = organizationId };
        var userGuid = Guid.NewGuid();
        var collectionId = Guid.NewGuid();

        var baseDetails = new List<OrganizationMemberBaseDetail>
        {
            // Same user, same collection, different ciphers
            new OrganizationMemberBaseDetail
            {
                UserGuid = userGuid,
                UserName = "Test User",
                Email = "test@example.com",
                AvatarColor = "#FF5733",
                TwoFactorProviders = "[]",
                UsesKeyConnector = false,
                ResetPasswordKey = null,
                CollectionId = collectionId,
                GroupId = null,
                GroupName = null,
                CollectionName = "Test Collection",
                ReadOnly = true,
                HidePasswords = true,
                Manage = false,
                CipherId = Guid.NewGuid()
            },
            new OrganizationMemberBaseDetail
            {
                UserGuid = userGuid,
                UserName = "Test User",
                Email = "test@example.com",
                AvatarColor = "#FF5733",
                TwoFactorProviders = "[]",
                UsesKeyConnector = false,
                ResetPasswordKey = null,
                CollectionId = collectionId,
                GroupId = null,
                GroupName = null,
                CollectionName = "Test Collection",
                ReadOnly = true,
                HidePasswords = true,
                Manage = false,
                CipherId = Guid.NewGuid()
            }
        };

        sutProvider.GetDependency<IOrganizationMemberBaseDetailRepository>()
            .GetOrganizationMemberBaseDetailsByOrganizationId(organizationId)
            .Returns(baseDetails);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)> { (userGuid, true) });

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { UseResetPassword = false });

        // Act
        var result = await sutProvider.Sut.GetMemberAccessReportsAsync(request);

        // Assert
        var resultList = result.ToList();

        // Should be grouped into one record since same user, collection, and permissions
        Assert.Single(resultList);

        var record = resultList[0];
        Assert.Equal(userGuid, record.UserGuid);
        Assert.Equal(collectionId, record.CollectionId);
        Assert.True(record.ReadOnly);
        Assert.True(record.HidePasswords);
        Assert.False(record.Manage);
        Assert.True(record.TwoFactorEnabled);

        // Should contain both cipher IDs
        Assert.Equal(2, record.CipherIds.Count());
    }

    [Theory]
    [BitAutoData]
    public async Task GetMemberAccessReportsAsync_MapsAccountRecoveryEnabled_WhenResetPasswordKeyPresent(
        SutProvider<MemberAccessReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        var request = new MemberAccessReportRequest { OrganizationId = organizationId };
        var userWithRecovery = Guid.NewGuid();
        var userWithoutRecovery = Guid.NewGuid();

        var baseDetails = new List<OrganizationMemberBaseDetail>
        {
            new OrganizationMemberBaseDetail
            {
                UserGuid = userWithRecovery,
                UserName = "User With Recovery",
                Email = "recovery@example.com",
                AvatarColor = "#FF0000",
                TwoFactorProviders = "[]",
                UsesKeyConnector = false,
                ResetPasswordKey = "some-reset-password-key",
                CollectionId = Guid.NewGuid(),
                GroupId = null,
                GroupName = null,
                CollectionName = "Test Collection",
                ReadOnly = false,
                HidePasswords = false,
                Manage = true,
                CipherId = Guid.NewGuid()
            },
            new OrganizationMemberBaseDetail
            {
                UserGuid = userWithoutRecovery,
                UserName = "User Without Recovery",
                Email = "norecovery@example.com",
                AvatarColor = "#00FF00",
                TwoFactorProviders = "[]",
                UsesKeyConnector = false,
                ResetPasswordKey = null,
                CollectionId = Guid.NewGuid(),
                GroupId = null,
                GroupName = null,
                CollectionName = "Test Collection",
                ReadOnly = false,
                HidePasswords = false,
                Manage = true,
                CipherId = Guid.NewGuid()
            }
        };

        sutProvider.GetDependency<IOrganizationMemberBaseDetailRepository>()
            .GetOrganizationMemberBaseDetailsByOrganizationId(organizationId)
            .Returns(baseDetails);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>
            {
                (userWithRecovery, false),
                (userWithoutRecovery, false)
            });

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { UseResetPassword = true });

        // Act
        var result = await sutProvider.Sut.GetMemberAccessReportsAsync(request);

        // Assert
        var resultList = result.ToList();
        Assert.Equal(2, resultList.Count);

        var withRecovery = resultList.First(r => r.UserGuid == userWithRecovery);
        var withoutRecovery = resultList.First(r => r.UserGuid == userWithoutRecovery);

        Assert.True(withRecovery.AccountRecoveryEnabled);
        Assert.False(withoutRecovery.AccountRecoveryEnabled);
    }

    [Theory]
    [BitAutoData]
    public async Task GetMemberAccessReportsAsync_MapsUsesKeyConnector_Correctly(
        SutProvider<MemberAccessReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        var request = new MemberAccessReportRequest { OrganizationId = organizationId };
        var userWithKeyConnector = Guid.NewGuid();

        var baseDetails = new List<OrganizationMemberBaseDetail>
        {
            new OrganizationMemberBaseDetail
            {
                UserGuid = userWithKeyConnector,
                UserName = "Key Connector User",
                Email = "keyconnector@example.com",
                AvatarColor = "#0000FF",
                TwoFactorProviders = "[]",
                UsesKeyConnector = true,
                ResetPasswordKey = null,
                CollectionId = Guid.NewGuid(),
                GroupId = null,
                GroupName = null,
                CollectionName = "Test Collection",
                ReadOnly = false,
                HidePasswords = false,
                Manage = true,
                CipherId = Guid.NewGuid()
            }
        };

        sutProvider.GetDependency<IOrganizationMemberBaseDetailRepository>()
            .GetOrganizationMemberBaseDetailsByOrganizationId(organizationId)
            .Returns(baseDetails);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>
            {
                (userWithKeyConnector, false)
            });

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { UseResetPassword = false });

        // Act
        var result = await sutProvider.Sut.GetMemberAccessReportsAsync(request);

        // Assert
        var resultList = result.ToList();
        Assert.Single(resultList);
        Assert.True(resultList[0].UsesKeyConnector);
    }

    [Theory]
    [BitAutoData]
    public async Task GetMemberAccessReportsAsync_SeparatesDirectAndGroupAccess_ForSameUser(
        SutProvider<MemberAccessReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        var request = new MemberAccessReportRequest { OrganizationId = organizationId };
        var userGuid = Guid.NewGuid();
        var directCollectionId = Guid.NewGuid();
        var groupCollectionId = Guid.NewGuid();

        var baseDetails = new List<OrganizationMemberBaseDetail>
        {
            // Direct access
            new OrganizationMemberBaseDetail
            {
                UserGuid = userGuid,
                UserName = "Test User",
                Email = "test@example.com",
                AvatarColor = "#FF5733",
                TwoFactorProviders = "[]",
                UsesKeyConnector = false,
                ResetPasswordKey = null,
                CollectionId = directCollectionId,
                GroupId = null,
                GroupName = null,
                CollectionName = "Direct Collection",
                ReadOnly = false,
                HidePasswords = false,
                Manage = true,
                CipherId = Guid.NewGuid()
            },
            // Group access
            new OrganizationMemberBaseDetail
            {
                UserGuid = userGuid,
                UserName = "Test User",
                Email = "test@example.com",
                AvatarColor = "#FF5733",
                TwoFactorProviders = "[]",
                UsesKeyConnector = false,
                ResetPasswordKey = null,
                CollectionId = groupCollectionId,
                GroupId = Guid.NewGuid(),
                GroupName = "Test Group",
                CollectionName = "Group Collection",
                ReadOnly = true,
                HidePasswords = false,
                Manage = false,
                CipherId = Guid.NewGuid()
            }
        };

        sutProvider.GetDependency<IOrganizationMemberBaseDetailRepository>()
            .GetOrganizationMemberBaseDetailsByOrganizationId(organizationId)
            .Returns(baseDetails);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)> { (userGuid, false) });

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { UseResetPassword = false });

        // Act
        var result = await sutProvider.Sut.GetMemberAccessReportsAsync(request);

        // Assert
        var resultList = result.ToList();
        Assert.Equal(2, resultList.Count);

        var directAccess = resultList.First(r => r.CollectionId == directCollectionId);
        var groupAccess = resultList.First(r => r.CollectionId == groupCollectionId);

        // Direct access should not have group info
        Assert.Null(directAccess.GroupId);
        Assert.Null(directAccess.GroupName);
        Assert.True(directAccess.Manage);
        Assert.False(directAccess.ReadOnly);

        // Group access should have group info
        Assert.NotNull(groupAccess.GroupId);
        Assert.Equal("Test Group", groupAccess.GroupName);
        Assert.False(groupAccess.Manage);
        Assert.True(groupAccess.ReadOnly);
    }

    [Theory]
    [BitAutoData]
    public async Task GetMemberAccessReportsAsync_WithNoMembers_ReturnsEmptyCollection(
        SutProvider<MemberAccessReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        var request = new MemberAccessReportRequest { OrganizationId = organizationId };

        sutProvider.GetDependency<IOrganizationMemberBaseDetailRepository>()
            .GetOrganizationMemberBaseDetailsByOrganizationId(organizationId)
            .Returns(new List<OrganizationMemberBaseDetail>());

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>());

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { UseResetPassword = false });

        // Act
        var result = await sutProvider.Sut.GetMemberAccessReportsAsync(request);

        // Assert
        Assert.Empty(result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetMemberAccessReportsAsync_WhenOrgAbilityIsNull_HandlesGracefully(
        SutProvider<MemberAccessReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        var request = new MemberAccessReportRequest { OrganizationId = organizationId };
        var userGuid = Guid.NewGuid();

        var baseDetails = new List<OrganizationMemberBaseDetail>
        {
            new OrganizationMemberBaseDetail
            {
                UserGuid = userGuid,
                UserName = "Test User",
                Email = "test@example.com",
                AvatarColor = "#FF5733",
                TwoFactorProviders = "[]",
                UsesKeyConnector = false,
                ResetPasswordKey = "some-key",
                CollectionId = Guid.NewGuid(),
                GroupId = null,
                GroupName = null,
                CollectionName = "Test Collection",
                ReadOnly = false,
                HidePasswords = false,
                Manage = true,
                CipherId = Guid.NewGuid()
            }
        };

        sutProvider.GetDependency<IOrganizationMemberBaseDetailRepository>()
            .GetOrganizationMemberBaseDetailsByOrganizationId(organizationId)
            .Returns(baseDetails);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)> { (userGuid, false) });

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns((OrganizationAbility)null);

        // Act & Assert - Should not throw
        var result = await sutProvider.Sut.GetMemberAccessReportsAsync(request);

        // Assert
        var resultList = result.ToList();
        Assert.Single(resultList);
        // Should default to false when orgAbility is null
        Assert.False(resultList[0].AccountRecoveryEnabled);
    }

    [Theory]
    [BitAutoData]
    public async Task GetMemberAccessReportsAsync_WithCollectionWithoutCiphers_ReturnsEmptyCipherIds(
        SutProvider<MemberAccessReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        var request = new MemberAccessReportRequest { OrganizationId = organizationId };
        var userGuid = Guid.NewGuid();
        var collectionId = Guid.NewGuid();

        var baseDetails = new List<OrganizationMemberBaseDetail>
        {
            // Collection access with null cipher (collection has no ciphers)
            new OrganizationMemberBaseDetail
            {
                UserGuid = userGuid,
                UserName = "Test User",
                Email = "test@example.com",
                AvatarColor = "#FF5733",
                TwoFactorProviders = "[]",
                UsesKeyConnector = false,
                ResetPasswordKey = null,
                CollectionId = collectionId,
                GroupId = null,
                GroupName = null,
                CollectionName = "Empty Collection",
                ReadOnly = false,
                HidePasswords = false,
                Manage = true,
                CipherId = null
            }
        };

        sutProvider.GetDependency<IOrganizationMemberBaseDetailRepository>()
            .GetOrganizationMemberBaseDetailsByOrganizationId(organizationId)
            .Returns(baseDetails);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)> { (userGuid, false) });

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { UseResetPassword = false });

        // Act
        var result = await sutProvider.Sut.GetMemberAccessReportsAsync(request);

        // Assert
        var resultList = result.ToList();
        Assert.Single(resultList);
        Assert.Empty(resultList[0].CipherIds);
    }

    [Theory]
    [BitAutoData]
    public async Task GetMemberAccessReportsAsync_WithUserInMultipleGroupsSameCollection_AggregatesCipherIds(
        SutProvider<MemberAccessReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        var request = new MemberAccessReportRequest { OrganizationId = organizationId };
        var userGuid = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        var group1Id = Guid.NewGuid();
        var group2Id = Guid.NewGuid();
        var cipher1 = Guid.NewGuid();
        var cipher2 = Guid.NewGuid();

        var baseDetails = new List<OrganizationMemberBaseDetail>
        {
            // Same user, same collection, group 1
            new OrganizationMemberBaseDetail
            {
                UserGuid = userGuid,
                UserName = "Test User",
                Email = "test@example.com",
                AvatarColor = "#FF5733",
                TwoFactorProviders = "[]",
                UsesKeyConnector = false,
                ResetPasswordKey = null,
                CollectionId = collectionId,
                GroupId = group1Id,
                GroupName = "Group 1",
                CollectionName = "Test Collection",
                ReadOnly = false,
                HidePasswords = false,
                Manage = true,
                CipherId = cipher1
            },
            // Same user, same collection, group 2
            new OrganizationMemberBaseDetail
            {
                UserGuid = userGuid,
                UserName = "Test User",
                Email = "test@example.com",
                AvatarColor = "#FF5733",
                TwoFactorProviders = "[]",
                UsesKeyConnector = false,
                ResetPasswordKey = null,
                CollectionId = collectionId,
                GroupId = group2Id,
                GroupName = "Group 2",
                CollectionName = "Test Collection",
                ReadOnly = false,
                HidePasswords = false,
                Manage = true,
                CipherId = cipher2
            }
        };

        sutProvider.GetDependency<IOrganizationMemberBaseDetailRepository>()
            .GetOrganizationMemberBaseDetailsByOrganizationId(organizationId)
            .Returns(baseDetails);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)> { (userGuid, false) });

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { UseResetPassword = false });

        // Act
        var result = await sutProvider.Sut.GetMemberAccessReportsAsync(request);

        // Assert
        var resultList = result.ToList();

        // Should create separate records for each group since GroupId is different
        Assert.Equal(2, resultList.Count);

        var group1Access = resultList.First(r => r.GroupId == group1Id);
        var group2Access = resultList.First(r => r.GroupId == group2Id);

        Assert.Equal("Group 1", group1Access.GroupName);
        Assert.Equal("Group 2", group2Access.GroupName);
        Assert.Single(group1Access.CipherIds);
        Assert.Single(group2Access.CipherIds);
    }

    [Theory]
    [BitAutoData]
    public async Task GetMemberAccessReportsAsync_WithMixedPermissions_GroupsCorrectly(
        SutProvider<MemberAccessReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        var request = new MemberAccessReportRequest { OrganizationId = organizationId };
        var userGuid = Guid.NewGuid();
        var collection1Id = Guid.NewGuid();
        var collection2Id = Guid.NewGuid();

        var baseDetails = new List<OrganizationMemberBaseDetail>
        {
            // Collection 1 - ReadOnly + HidePasswords
            new OrganizationMemberBaseDetail
            {
                UserGuid = userGuid,
                UserName = "Test User",
                Email = "test@example.com",
                AvatarColor = "#FF5733",
                TwoFactorProviders = "[]",
                UsesKeyConnector = false,
                ResetPasswordKey = null,
                CollectionId = collection1Id,
                GroupId = null,
                GroupName = null,
                CollectionName = "Collection 1",
                ReadOnly = true,
                HidePasswords = true,
                Manage = false,
                CipherId = Guid.NewGuid()
            },
            // Collection 2 - Manage access
            new OrganizationMemberBaseDetail
            {
                UserGuid = userGuid,
                UserName = "Test User",
                Email = "test@example.com",
                AvatarColor = "#FF5733",
                TwoFactorProviders = "[]",
                UsesKeyConnector = false,
                ResetPasswordKey = null,
                CollectionId = collection2Id,
                GroupId = null,
                GroupName = null,
                CollectionName = "Collection 2",
                ReadOnly = false,
                HidePasswords = false,
                Manage = true,
                CipherId = Guid.NewGuid()
            }
        };

        sutProvider.GetDependency<IOrganizationMemberBaseDetailRepository>()
            .GetOrganizationMemberBaseDetailsByOrganizationId(organizationId)
            .Returns(baseDetails);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)> { (userGuid, false) });

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { UseResetPassword = false });

        // Act
        var result = await sutProvider.Sut.GetMemberAccessReportsAsync(request);

        // Assert
        var resultList = result.ToList();
        Assert.Equal(2, resultList.Count);

        var collection1 = resultList.First(r => r.CollectionId == collection1Id);
        var collection2 = resultList.First(r => r.CollectionId == collection2Id);

        Assert.True(collection1.ReadOnly);
        Assert.True(collection1.HidePasswords);
        Assert.False(collection1.Manage);

        Assert.False(collection2.ReadOnly);
        Assert.False(collection2.HidePasswords);
        Assert.True(collection2.Manage);
    }

    [Theory]
    [BitAutoData]
    public async Task GetMemberAccessReportsAsync_WhenTwoFactorQueryReturnsPartialResults_DefaultsToFalse(
        SutProvider<MemberAccessReportQuery> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = fixture.Create<Guid>();
        var request = new MemberAccessReportRequest { OrganizationId = organizationId };
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        var baseDetails = new List<OrganizationMemberBaseDetail>
        {
            new OrganizationMemberBaseDetail
            {
                UserGuid = user1,
                UserName = "User 1",
                Email = "user1@example.com",
                AvatarColor = "#FF0000",
                TwoFactorProviders = "[]",
                UsesKeyConnector = false,
                ResetPasswordKey = null,
                CollectionId = Guid.NewGuid(),
                GroupId = null,
                GroupName = null,
                CollectionName = "Collection 1",
                ReadOnly = false,
                HidePasswords = false,
                Manage = true,
                CipherId = Guid.NewGuid()
            },
            new OrganizationMemberBaseDetail
            {
                UserGuid = user2,
                UserName = "User 2",
                Email = "user2@example.com",
                AvatarColor = "#00FF00",
                TwoFactorProviders = "[]",
                UsesKeyConnector = false,
                ResetPasswordKey = null,
                CollectionId = Guid.NewGuid(),
                GroupId = null,
                GroupName = null,
                CollectionName = "Collection 2",
                ReadOnly = false,
                HidePasswords = false,
                Manage = true,
                CipherId = Guid.NewGuid()
            }
        };

        sutProvider.GetDependency<IOrganizationMemberBaseDetailRepository>()
            .GetOrganizationMemberBaseDetailsByOrganizationId(organizationId)
            .Returns(baseDetails);

        // Only return 2FA status for user1, not user2
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>
            {
                (user1, true)
                // user2 is missing from results
            });

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { UseResetPassword = false });

        // Act
        var result = await sutProvider.Sut.GetMemberAccessReportsAsync(request);

        // Assert
        var resultList = result.ToList();
        Assert.Equal(2, resultList.Count);

        var user1Result = resultList.First(r => r.UserGuid == user1);
        var user2Result = resultList.First(r => r.UserGuid == user2);

        Assert.True(user1Result.TwoFactorEnabled);
        // User2 should default to false when not in TwoFactorQuery results
        Assert.False(user2Result.TwoFactorEnabled);
    }
}
