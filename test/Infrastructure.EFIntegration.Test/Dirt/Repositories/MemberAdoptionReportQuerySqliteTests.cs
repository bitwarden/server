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
/// Covers the three reads the member adoption report is built from, against a real, throwaway SQLite
/// database. The two edge reads feed <see cref="Bit.Core.Dirt.Reports.ReportFeatures.SharedItemCountCalculator"/>,
/// so what matters here is the edge sets themselves, not the counts derived from them.
/// </summary>
public class MemberAdoptionReportQuerySqliteTests : IDisposable
{
    private static readonly DateTime _activityDate = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _deletionDate = new(2026, 1, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly DatabaseContext _dbContext;

    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _otherOrganizationId = Guid.NewGuid();

    private readonly Guid _memberUserId = Guid.NewGuid();
    private readonly Guid _memberOrganizationUserId = Guid.NewGuid();

    // Both of these members have no linked user and no invite email, so both sort as the empty string
    // and only the organization user id breaks the tie. The values are chosen so that .NET and SQLite
    // agree on their order.
    private readonly Guid _firstUserlessOrganizationUserId = new("00000000-0000-0000-0000-0000000000a1");
    private readonly Guid _secondUserlessOrganizationUserId = new("00000000-0000-0000-0000-0000000000a2");
    private readonly Guid _invitedEmailOrganizationUserId = Guid.NewGuid();

    private readonly Guid _bothPathsCollectionId = Guid.NewGuid();
    private readonly Guid _directOnlyCollectionId = Guid.NewGuid();
    private readonly Guid _groupOnlyCollectionId = Guid.NewGuid();
    private readonly Guid _twoGroupsCollectionId = Guid.NewGuid();
    private readonly Guid _otherOrganizationCollectionId = Guid.NewGuid();

    private readonly Guid _firstGroupId = Guid.NewGuid();
    private readonly Guid _secondGroupId = Guid.NewGuid();

    private Guid _sharedCipherId;
    private Guid _deletedCipherId;
    private Guid _personalCipherInCollectionId;
    private Guid _otherOrganizationCipherId;

    public MemberAdoptionReportQuerySqliteTests()
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
    }

    [Fact]
    public async Task GetMemberCollectionAccessAsync_CollapsesDirectAndGroupGrantsToOneEdgePerCollection()
    {
        var access = await MemberAdoptionReportRepository.GetMemberCollectionAccessAsync(
            _dbContext, _organizationId);

        Assert.Equal(
            new HashSet<MemberCollectionAccess>
            {
                new(_memberOrganizationUserId, _bothPathsCollectionId),
                new(_memberOrganizationUserId, _directOnlyCollectionId),
                new(_memberOrganizationUserId, _groupOnlyCollectionId),
                new(_memberOrganizationUserId, _twoGroupsCollectionId)
            },
            access.ToHashSet());

        // A set that is already distinct is the calculator's contract, so the list must not merely
        // contain the right edges, it must contain each of them once.
        Assert.Equal(4, access.Count);
    }

    [Fact]
    public async Task GetMemberCollectionAccessAsync_IgnoresCollectionsOwnedByAnotherOrganization()
    {
        var access = await MemberAdoptionReportRepository.GetMemberCollectionAccessAsync(
            _dbContext, _organizationId);

        Assert.DoesNotContain(access, edge => edge.CollectionId == _otherOrganizationCollectionId);
    }

    [Fact]
    public async Task GetCollectionCipherLinksAsync_ReturnsOnlyLiveOrganizationOwnedCiphers()
    {
        var links = await MemberAdoptionReportRepository.GetCollectionCipherLinksAsync(
            _dbContext, _organizationId);

        Assert.Equal(
            new HashSet<CollectionCipherLink>
            {
                new(_bothPathsCollectionId, _sharedCipherId),
                new(_directOnlyCollectionId, _sharedCipherId)
            },
            links.ToHashSet());
        Assert.Equal(2, links.Count);
    }

    [Fact]
    public async Task GetCollectionCipherLinksAsync_ExcludesDeletedPersonalAndForeignCiphers()
    {
        var links = await MemberAdoptionReportRepository.GetCollectionCipherLinksAsync(
            _dbContext, _organizationId);

        Assert.DoesNotContain(links, link => link.CipherId == _deletedCipherId);
        Assert.DoesNotContain(links, link => link.CipherId == _personalCipherInCollectionId);
        Assert.DoesNotContain(links, link => link.CipherId == _otherOrganizationCipherId);
    }

    [Fact]
    public async Task GetConfirmedMemberDetailsAsync_OrdersByCoalescedEmailThenOrganizationUserId()
    {
        var details = await MemberAdoptionReportRepository.GetConfirmedMemberDetailsAsync(
            _dbContext, _organizationId);

        Assert.Equal(
            new[]
            {
                _firstUserlessOrganizationUserId,
                _secondUserlessOrganizationUserId,
                _invitedEmailOrganizationUserId,
                _memberOrganizationUserId
            },
            details.Select(detail => detail.OrganizationUserId));
        Assert.Equal(new[] { "", "", "invited@example.com", "member@example.com" },
            details.Select(detail => detail.Email));
    }

    [Fact]
    public async Task GetConfirmedMemberDetailsAsync_ReportsNothingPersonalForAMemberWithNoLinkedUser()
    {
        var details = await MemberAdoptionReportRepository.GetConfirmedMemberDetailsAsync(
            _dbContext, _organizationId);

        var userless = details.Single(
            detail => detail.OrganizationUserId == _firstUserlessOrganizationUserId);

        Assert.Null(userless.UserId);
        Assert.Null(userless.Name);
        Assert.Null(userless.LastActivityDate);
        Assert.False(userless.HasExtensionInstalled);
        // An unowned cipher exists; it must not be attributed to every member without a user.
        Assert.Equal(0, userless.VaultItemCount);
    }

    [Fact]
    public async Task GetConfirmedMemberDetailsAsync_CountsOnlyTheMembersOwnLivePersonalItems()
    {
        var details = await MemberAdoptionReportRepository.GetConfirmedMemberDetailsAsync(
            _dbContext, _organizationId);

        var member = details.Single(detail => detail.OrganizationUserId == _memberOrganizationUserId);

        Assert.Equal(1, member.VaultItemCount);
        Assert.Equal(_activityDate, member.LastActivityDate);
        Assert.True(member.HasExtensionInstalled);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Seed()
    {
        _dbContext.Organizations.Add(BuildOrganization(_organizationId, "Reported organization"));
        _dbContext.Organizations.Add(BuildOrganization(_otherOrganizationId, "Other organization"));

        _dbContext.Users.Add(new EfModel.User
        {
            Id = _memberUserId,
            Name = "Member",
            Email = "member@example.com",
            SecurityStamp = Guid.NewGuid().ToString(),
            ApiKey = Guid.NewGuid().ToString("N")[..30],
            CreationDate = _activityDate,
            RevisionDate = _activityDate,
            AccountRevisionDate = _activityDate
        });

        _dbContext.OrganizationUsers.Add(BuildOrganizationUser(_memberOrganizationUserId, _memberUserId, null));
        _dbContext.OrganizationUsers.Add(BuildOrganizationUser(_firstUserlessOrganizationUserId, null, null));
        _dbContext.OrganizationUsers.Add(BuildOrganizationUser(_secondUserlessOrganizationUserId, null, null));
        _dbContext.OrganizationUsers.Add(
            BuildOrganizationUser(_invitedEmailOrganizationUserId, null, "invited@example.com"));

        _dbContext.Devices.Add(new EfModel.Device
        {
            Id = Guid.NewGuid(),
            UserId = _memberUserId,
            Name = "Chrome",
            Identifier = Guid.NewGuid().ToString(),
            Type = DeviceType.ChromeExtension,
            LastActivityDate = _activityDate
        });

        _dbContext.Collections.Add(BuildCollection(_bothPathsCollectionId, _organizationId, "Both paths"));
        _dbContext.Collections.Add(BuildCollection(_directOnlyCollectionId, _organizationId, "Direct only"));
        _dbContext.Collections.Add(BuildCollection(_groupOnlyCollectionId, _organizationId, "Group only"));
        _dbContext.Collections.Add(BuildCollection(_twoGroupsCollectionId, _organizationId, "Two groups"));
        _dbContext.Collections.Add(
            BuildCollection(_otherOrganizationCollectionId, _otherOrganizationId, "Other organization"));

        _dbContext.Groups.Add(BuildGroup(_firstGroupId, "Engineering"));
        _dbContext.Groups.Add(BuildGroup(_secondGroupId, "Support"));
        AddGroupUser(_firstGroupId, _memberOrganizationUserId);
        AddGroupUser(_secondGroupId, _memberOrganizationUserId);

        AddCollectionUser(_bothPathsCollectionId, _memberOrganizationUserId);
        AddCollectionUser(_directOnlyCollectionId, _memberOrganizationUserId);
        AddCollectionUser(_otherOrganizationCollectionId, _memberOrganizationUserId);

        AddCollectionGroup(_bothPathsCollectionId, _firstGroupId);
        AddCollectionGroup(_groupOnlyCollectionId, _firstGroupId);
        AddCollectionGroup(_twoGroupsCollectionId, _firstGroupId);
        AddCollectionGroup(_twoGroupsCollectionId, _secondGroupId);
        AddCollectionGroup(_otherOrganizationCollectionId, _firstGroupId);

        _sharedCipherId = AddCipher(userId: null, organizationId: _organizationId);
        _deletedCipherId = AddCipher(userId: null, organizationId: _organizationId, deletedDate: _deletionDate);
        _personalCipherInCollectionId = AddCipher(userId: _memberUserId, organizationId: null);
        _otherOrganizationCipherId = AddCipher(userId: null, organizationId: _otherOrganizationId);

        AddCollectionCipher(_bothPathsCollectionId, _sharedCipherId);
        AddCollectionCipher(_directOnlyCollectionId, _sharedCipherId);
        AddCollectionCipher(_bothPathsCollectionId, _deletedCipherId);
        AddCollectionCipher(_bothPathsCollectionId, _personalCipherInCollectionId);
        AddCollectionCipher(_otherOrganizationCollectionId, _otherOrganizationCipherId);

        AddCipher(userId: _memberUserId, organizationId: null, deletedDate: _deletionDate);
        AddCipher(userId: _memberUserId, organizationId: _organizationId);
        // Owned by nobody, so no member's personal vault count may pick it up.
        AddCipher(userId: null, organizationId: null);

        _dbContext.SaveChanges();
    }

    private EfAdminConsoleModel.Organization BuildOrganization(Guid id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            BillingEmail = $"{id}@example.com",
            Plan = "Enterprise",
            Enabled = true,
            CreationDate = _activityDate,
            RevisionDate = _activityDate
        };

    private EfModel.OrganizationUser BuildOrganizationUser(Guid id, Guid? userId, string? email) =>
        new()
        {
            Id = id,
            OrganizationId = _organizationId,
            UserId = userId,
            Email = email,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            RevisionDate = _activityDate
        };

    private EfAdminConsoleModel.Collection BuildCollection(Guid id, Guid organizationId, string name) =>
        new()
        {
            Id = id,
            OrganizationId = organizationId,
            Name = name,
            Type = CollectionType.SharedCollection,
            CreationDate = _activityDate,
            RevisionDate = _activityDate
        };

    private EfModel.Group BuildGroup(Guid id, string name) =>
        new()
        {
            Id = id,
            OrganizationId = _organizationId,
            Name = name,
            RevisionDate = _activityDate
        };

    private void AddGroupUser(Guid groupId, Guid organizationUserId) =>
        _dbContext.GroupUsers.Add(new EfModel.GroupUser
        {
            GroupId = groupId,
            OrganizationUserId = organizationUserId
        });

    private void AddCollectionUser(Guid collectionId, Guid organizationUserId) =>
        _dbContext.CollectionUsers.Add(new EfAdminConsoleModel.CollectionUser
        {
            CollectionId = collectionId,
            OrganizationUserId = organizationUserId
        });

    private void AddCollectionGroup(Guid collectionId, Guid groupId) =>
        _dbContext.CollectionGroups.Add(new EfAdminConsoleModel.CollectionGroup
        {
            CollectionId = collectionId,
            GroupId = groupId
        });

    private void AddCollectionCipher(Guid collectionId, Guid cipherId) =>
        _dbContext.CollectionCiphers.Add(new EfModel.CollectionCipher
        {
            CollectionId = collectionId,
            CipherId = cipherId
        });

    private Guid AddCipher(Guid? userId, Guid? organizationId, DateTime? deletedDate = null)
    {
        var cipher = new EfVaultModel.Cipher
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            Type = CipherType.Login,
            Data = "{}",
            CreationDate = _activityDate,
            RevisionDate = _activityDate,
            DeletedDate = deletedDate
        };

        _dbContext.Ciphers.Add(cipher);
        return cipher.Id;
    }

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
}
