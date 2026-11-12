using AutoMapper;
using Bit.Core;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Enums;
using Bit.Core.Vault.Enums;
using Bit.Infrastructure.EntityFramework.Dirt.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using EfAdminConsoleModel = Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using EfModel = Bit.Infrastructure.EntityFramework.Models;
using EfVaultModel = Bit.Infrastructure.EntityFramework.Vault.Models;

namespace Bit.Infrastructure.EFIntegration.Test.Dirt.Repositories;

/// <summary>
/// Runs the member adoption report against a real, throwaway SQLite database.
/// </summary>
public class MemberAdoptionReportRepositorySqliteTests : IDisposable
{
    private static readonly DateTime _olderActivityDate = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _latestActivityDate = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _deletionDate = new(2026, 1, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly DatabaseContext _dbContext;
    private readonly MemberAdoptionReportRepository _sut;

    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _sponsoredOrganizationId = Guid.NewGuid();
    private readonly Guid _memberUserId = Guid.NewGuid();
    private readonly Guid _memberOrganizationUserId = Guid.NewGuid();
    private readonly Guid _memberWithoutAccessUserId = Guid.NewGuid();
    private readonly Guid _memberWithoutAccessOrganizationUserId = Guid.NewGuid();
    private readonly Guid _invitedUserId = Guid.NewGuid();
    private readonly Guid _invitedOrganizationUserId = Guid.NewGuid();

    public MemberAdoptionReportRepositorySqliteTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlite(_connection)
            .UseApplicationServiceProvider(BuildDataProtectionServiceProvider())
            .Options;

        _dbContext = new DatabaseContext(options);
        _dbContext.Database.EnsureCreated();

        Seed();

        _sut = new MemberAdoptionReportRepository(
            new SingleContextScopeFactory(_dbContext), Substitute.For<IMapper>());
    }

    [Fact]
    public async Task GetMemberAdoptionDetailsByOrganizationIdAsync_ReturnsEveryConfirmedMemberAndNoOthers()
    {
        var details = (await _sut.GetMemberAdoptionDetailsByOrganizationIdAsync(_organizationId)).ToList();

        Assert.Equal(2, details.Count);
        Assert.DoesNotContain(details, detail => detail.OrganizationUserId == _invitedOrganizationUserId);
        Assert.Contains(details, detail => detail.OrganizationUserId == _memberOrganizationUserId);
        Assert.Contains(details, detail => detail.OrganizationUserId == _memberWithoutAccessOrganizationUserId);
    }

    [Fact]
    public async Task GetMemberAdoptionDetailsByOrganizationIdAsync_ReportsExtensionInstalledPerMember()
    {
        var (member, memberWithoutAccess) = await GetMembersAsync();

        Assert.True(member.HasExtensionInstalled);
        Assert.False(memberWithoutAccess.HasExtensionInstalled);
    }

    [Fact]
    public async Task GetMemberAdoptionDetailsByOrganizationIdAsync_TakesTheLatestActivityAcrossAllDeviceTypes()
    {
        var (member, memberWithoutAccess) = await GetMembersAsync();

        Assert.Equal(_latestActivityDate, member.LastActivityDate);
        Assert.Equal(_olderActivityDate, memberWithoutAccess.LastActivityDate);
    }

    [Fact]
    public async Task GetMemberAdoptionDetailsByOrganizationIdAsync_CountsOnlyLivePersonalItemsAsVaultItems()
    {
        var (member, memberWithoutAccess) = await GetMembersAsync();

        Assert.Equal(1, member.VaultItemCount);
        Assert.Equal(0, memberWithoutAccess.VaultItemCount);
    }

    [Fact]
    public async Task GetMemberAdoptionDetailsByOrganizationIdAsync_CountsItemsReachableByBothAccessPathsOnce()
    {
        var (member, memberWithoutAccess) = await GetMembersAsync();

        Assert.Equal(3, member.SharedItemCount);
        Assert.Equal(0, memberWithoutAccess.SharedItemCount);
    }

    [Fact]
    public async Task GetMemberAdoptionDetailsByOrganizationIdAsync_ReportsSponsorshipOnlyOnceRedeemed()
    {
        var (member, memberWithoutAccess) = await GetMembersAsync();

        Assert.True(member.HasRedeemedSponsorship);
        Assert.False(memberWithoutAccess.HasRedeemedSponsorship);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<(MemberAdoptionReportDetail Member, MemberAdoptionReportDetail MemberWithoutAccess)>
        GetMembersAsync()
    {
        var details = (await _sut.GetMemberAdoptionDetailsByOrganizationIdAsync(_organizationId)).ToList();

        return (
            details.Single(detail => detail.OrganizationUserId == _memberOrganizationUserId),
            details.Single(detail => detail.OrganizationUserId == _memberWithoutAccessOrganizationUserId));
    }

    private void Seed()
    {
        _dbContext.Organizations.Add(BuildOrganization(_organizationId, "Reported organization"));
        _dbContext.Organizations.Add(BuildOrganization(_sponsoredOrganizationId, "Sponsored organization"));

        _dbContext.Users.Add(BuildUser(_memberUserId, "member@example.com"));
        _dbContext.Users.Add(BuildUser(_memberWithoutAccessUserId, "no-access@example.com"));
        _dbContext.Users.Add(BuildUser(_invitedUserId, "invited@example.com"));

        _dbContext.OrganizationUsers.Add(BuildOrganizationUser(
            _memberOrganizationUserId, _memberUserId, OrganizationUserStatusType.Confirmed));
        _dbContext.OrganizationUsers.Add(BuildOrganizationUser(
            _memberWithoutAccessOrganizationUserId, _memberWithoutAccessUserId, OrganizationUserStatusType.Confirmed));
        _dbContext.OrganizationUsers.Add(BuildOrganizationUser(
            _invitedOrganizationUserId, _invitedUserId, OrganizationUserStatusType.Invited));

        // The member's newest activity is on a desktop, not the extension.
        _dbContext.Devices.Add(BuildDevice(_memberUserId, DeviceType.ChromeExtension, _olderActivityDate));
        _dbContext.Devices.Add(BuildDevice(_memberUserId, DeviceType.MacOsDesktop, _latestActivityDate));
        _dbContext.Devices.Add(BuildDevice(_memberWithoutAccessUserId, DeviceType.MacOsDesktop, _olderActivityDate));

        var sharedCollectionId = Guid.NewGuid();
        var groupOnlyCollectionId = Guid.NewGuid();
        var directOnlyCollectionId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _dbContext.Collections.Add(BuildCollection(sharedCollectionId, "Shared"));
        _dbContext.Collections.Add(BuildCollection(groupOnlyCollectionId, "Group only"));
        _dbContext.Collections.Add(BuildCollection(directOnlyCollectionId, "Direct only"));

        _dbContext.Groups.Add(new EfModel.Group
        {
            Id = groupId,
            OrganizationId = _organizationId,
            Name = "Engineering",
            RevisionDate = _olderActivityDate
        });
        _dbContext.GroupUsers.Add(new EfModel.GroupUser
        {
            GroupId = groupId,
            OrganizationUserId = _memberOrganizationUserId
        });

        _dbContext.CollectionUsers.Add(new EfAdminConsoleModel.CollectionUser
        {
            CollectionId = sharedCollectionId,
            OrganizationUserId = _memberOrganizationUserId
        });
        _dbContext.CollectionUsers.Add(new EfAdminConsoleModel.CollectionUser
        {
            CollectionId = directOnlyCollectionId,
            OrganizationUserId = _memberOrganizationUserId
        });
        _dbContext.CollectionGroups.Add(new EfAdminConsoleModel.CollectionGroup
        {
            CollectionId = sharedCollectionId,
            GroupId = groupId
        });
        _dbContext.CollectionGroups.Add(new EfAdminConsoleModel.CollectionGroup
        {
            CollectionId = groupOnlyCollectionId,
            GroupId = groupId
        });

        var cipherReachableByBothPaths = AddOrganizationCipher();
        var cipherReachableByDirectAccessOnly = AddOrganizationCipher();
        var cipherReachableByGroupAccessOnly = AddOrganizationCipher();
        var deletedCipher = AddOrganizationCipher(_deletionDate);

        AddCollectionCipher(sharedCollectionId, cipherReachableByBothPaths);
        AddCollectionCipher(groupOnlyCollectionId, cipherReachableByBothPaths);
        AddCollectionCipher(directOnlyCollectionId, cipherReachableByDirectAccessOnly);
        AddCollectionCipher(groupOnlyCollectionId, cipherReachableByGroupAccessOnly);
        AddCollectionCipher(sharedCollectionId, deletedCipher);

        _dbContext.Ciphers.Add(BuildCipher(_memberUserId, organizationId: null));
        _dbContext.Ciphers.Add(BuildCipher(_memberUserId, organizationId: null, deletedDate: _deletionDate));
        _dbContext.Ciphers.Add(BuildCipher(_memberUserId, organizationId: _organizationId));

        _dbContext.OrganizationSponsorships.Add(new EfModel.OrganizationSponsorship
        {
            Id = Guid.NewGuid(),
            SponsoringOrganizationId = _organizationId,
            SponsoringOrganizationUserId = _memberOrganizationUserId,
            SponsoredOrganizationId = _sponsoredOrganizationId,
            OfferedToEmail = "friend@example.com"
        });
        _dbContext.OrganizationSponsorships.Add(new EfModel.OrganizationSponsorship
        {
            Id = Guid.NewGuid(),
            SponsoringOrganizationId = _organizationId,
            SponsoringOrganizationUserId = _memberWithoutAccessOrganizationUserId,
            SponsoredOrganizationId = null,
            OfferedToEmail = "invitee@example.com"
        });

        _dbContext.SaveChanges();
    }

    private Guid AddOrganizationCipher(DateTime? deletedDate = null)
    {
        var cipher = BuildCipher(userId: null, organizationId: _organizationId, deletedDate: deletedDate);
        _dbContext.Ciphers.Add(cipher);
        return cipher.Id;
    }

    private void AddCollectionCipher(Guid collectionId, Guid cipherId) =>
        _dbContext.CollectionCiphers.Add(new EfModel.CollectionCipher
        {
            CollectionId = collectionId,
            CipherId = cipherId
        });

    private EfAdminConsoleModel.Organization BuildOrganization(Guid id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            BillingEmail = $"{id}@example.com",
            Plan = "Enterprise",
            Enabled = true,
            CreationDate = _olderActivityDate,
            RevisionDate = _olderActivityDate
        };

    private EfModel.User BuildUser(Guid id, string email) =>
        new()
        {
            Id = id,
            Name = email,
            Email = email,
            SecurityStamp = Guid.NewGuid().ToString(),
            ApiKey = Guid.NewGuid().ToString("N")[..30],
            CreationDate = _olderActivityDate,
            RevisionDate = _olderActivityDate,
            AccountRevisionDate = _olderActivityDate
        };

    private EfModel.OrganizationUser BuildOrganizationUser(
        Guid id, Guid userId, OrganizationUserStatusType status) =>
        new()
        {
            Id = id,
            OrganizationId = _organizationId,
            UserId = userId,
            Status = status,
            Type = OrganizationUserType.User,
            RevisionDate = _olderActivityDate
        };

    private EfModel.Device BuildDevice(Guid userId, DeviceType type, DateTime lastActivityDate) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = type.ToString(),
            Identifier = Guid.NewGuid().ToString(),
            Type = type,
            LastActivityDate = lastActivityDate
        };

    private EfAdminConsoleModel.Collection BuildCollection(Guid id, string name) =>
        new()
        {
            Id = id,
            OrganizationId = _organizationId,
            Name = name,
            Type = CollectionType.SharedCollection,
            CreationDate = _olderActivityDate,
            RevisionDate = _olderActivityDate
        };

    private EfVaultModel.Cipher BuildCipher(Guid? userId, Guid? organizationId, DateTime? deletedDate = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            Type = CipherType.Login,
            Data = "{}",
            CreationDate = _olderActivityDate,
            RevisionDate = _olderActivityDate,
            DeletedDate = deletedDate
        };

    private static IServiceProvider BuildDataProtectionServiceProvider() =>
        new ServiceCollection()
            .AddSingleton(_ =>
            {
                var dataProtector = Substitute.For<IDataProtector>();
                dataProtector.Protect(Arg.Any<byte[]>()).Returns<byte[]>(data => (byte[])data[0]);
                dataProtector.Unprotect(Arg.Any<byte[]>()).Returns<byte[]>(data => (byte[])data[0]);

                var dataProtectionProvider = Substitute.For<IDataProtectionProvider>();
                dataProtectionProvider.CreateProtector(Constants.DatabaseFieldProtectorPurpose)
                    .Returns(dataProtector);

                return dataProtectionProvider;
            })
            .BuildServiceProvider();

    private sealed class SingleContextScopeFactory : IServiceScopeFactory, IServiceScope, IServiceProvider
    {
        private readonly DatabaseContext _dbContext;

        public SingleContextScopeFactory(DatabaseContext dbContext)
        {
            _dbContext = dbContext;
        }

        public IServiceProvider ServiceProvider => this;

        public IServiceScope CreateScope() => this;

        public object? GetService(Type serviceType) =>
            serviceType == typeof(DatabaseContext) ? _dbContext : null;

        public void Dispose()
        {
        }
    }
}
