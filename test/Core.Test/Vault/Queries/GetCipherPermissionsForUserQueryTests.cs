﻿using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Queries;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Vault.Queries;

[SutProviderCustomize]
public class GetCipherPermissionsForUserQueryTests
{
    private static Guid _noAccessCipherId = Guid.NewGuid();
    private static Guid _readOnlyCipherId = Guid.NewGuid();
    private static Guid _editCipherId = Guid.NewGuid();
    private static Guid _manageCipherId = Guid.NewGuid();
    private static Guid _readExceptPasswordCipherId = Guid.NewGuid();
    private static Guid _unassignedCipherId = Guid.NewGuid();

    private static List<Guid> _cipherIds = new[]
    {
        _noAccessCipherId,
        _readOnlyCipherId,
        _editCipherId,
        _manageCipherId,
        _readExceptPasswordCipherId,
        _unassignedCipherId
    }.ToList();


    [Theory, BitAutoData]
    public async Task GetCipherPermissionsForUserQuery_Base(Guid userId, CurrentContextOrganization org, SutProvider<GetCipherPermissionsForUserQuery> sutProvider
    )
    {
        var organizationId = org.Id;
        org.Type = OrganizationUserType.User;
        org.Permissions.EditAnyCollection = false;
        var cipherPermissions = CreateCipherPermissions();

        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(org);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);

        sutProvider.GetDependency<ICipherRepository>().GetCipherPermissionsForOrganizationAsync(organizationId, userId)
            .Returns(cipherPermissions);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyUnassignedOrganizationDetailsByOrganizationIdAsync(organizationId)
            .Returns(new List<CipherOrganizationDetails>
            {
                new() { Id = _unassignedCipherId }
            });

        var result = await sutProvider.Sut.GetByOrganization(organizationId);

        Assert.Equal(6, result.Count);
        Assert.All(result, x => Assert.Contains(x.Key, _cipherIds));
        Assert.False(result[_noAccessCipherId].Read);
        Assert.True(result[_readOnlyCipherId].Read);
        Assert.False(result[_readOnlyCipherId].Edit);
        Assert.True(result[_editCipherId].Edit);
        Assert.True(result[_manageCipherId].Manage);
        Assert.True(result[_readExceptPasswordCipherId].Read);
        Assert.False(result[_readExceptPasswordCipherId].ViewPassword);
        Assert.False(result[_unassignedCipherId].Read);
    }

    [Theory, BitAutoData]
    public async Task GetCipherPermissionsForUserQuery_CanEditAllCiphers_CustomUser(Guid userId, CurrentContextOrganization org, SutProvider<GetCipherPermissionsForUserQuery> sutProvider
    )
    {
        var organizationId = org.Id;
        var cipherPermissions = CreateCipherPermissions();
        org.Permissions.EditAnyCollection = true;
        org.Type = OrganizationUserType.Custom;

        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(org);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);

        sutProvider.GetDependency<ICipherRepository>().GetCipherPermissionsForOrganizationAsync(organizationId, userId)
            .Returns(cipherPermissions);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyUnassignedOrganizationDetailsByOrganizationIdAsync(organizationId)
            .Returns(new List<CipherOrganizationDetails>
            {
                new() { Id = _unassignedCipherId }
            });

        var result = await sutProvider.Sut.GetByOrganization(organizationId);

        Assert.Equal(6, result.Count);
        Assert.All(result, x => Assert.Contains(x.Key, _cipherIds));
        Assert.All(result, x => Assert.True(x.Value.Read && x.Value.Edit && x.Value.Manage && x.Value.ViewPassword));
    }

    [Theory, BitAutoData]
    public async Task GetCipherPermissionsForUserQuery_CanEditAllCiphers_Admin(Guid userId, CurrentContextOrganization org, SutProvider<GetCipherPermissionsForUserQuery> sutProvider
    )
    {
        var organizationId = org.Id;
        var cipherPermissions = CreateCipherPermissions();
        org.Permissions.EditAnyCollection = false;
        org.Type = OrganizationUserType.Admin;

        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(org);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);

        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(org.Id).Returns(new OrganizationAbility
        {
            AllowAdminAccessToAllCollectionItems = true
        });

        sutProvider.GetDependency<ICipherRepository>().GetCipherPermissionsForOrganizationAsync(organizationId, userId)
            .Returns(cipherPermissions);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyUnassignedOrganizationDetailsByOrganizationIdAsync(organizationId)
            .Returns(new List<CipherOrganizationDetails>
            {
                new() { Id = _unassignedCipherId }
            });

        var result = await sutProvider.Sut.GetByOrganization(organizationId);

        Assert.Equal(6, result.Count);
        Assert.All(result, x => Assert.Contains(x.Key, _cipherIds));
        Assert.All(result, x => Assert.True(x.Value.Read && x.Value.Edit && x.Value.Manage && x.Value.ViewPassword));
    }

    [Theory, BitAutoData]
    public async Task GetCipherPermissionsForUserQuery_CanEditUnassignedCiphers(Guid userId, CurrentContextOrganization org, SutProvider<GetCipherPermissionsForUserQuery> sutProvider
    )
    {
        var organizationId = org.Id;
        var cipherPermissions = CreateCipherPermissions();
        org.Type = OrganizationUserType.Owner;
        org.Permissions.EditAnyCollection = false;

        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(org);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);

        sutProvider.GetDependency<ICipherRepository>().GetCipherPermissionsForOrganizationAsync(organizationId, userId)
            .Returns(cipherPermissions);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyUnassignedOrganizationDetailsByOrganizationIdAsync(organizationId)
            .Returns(new List<CipherOrganizationDetails>
            {
                new() { Id = _unassignedCipherId }
            });

        var result = await sutProvider.Sut.GetByOrganization(organizationId);

        Assert.Equal(6, result.Count);
        Assert.All(result, x => Assert.Contains(x.Key, _cipherIds));
        Assert.False(result[_noAccessCipherId].Read);
        Assert.True(result[_readOnlyCipherId].Read);
        Assert.False(result[_readOnlyCipherId].Edit);
        Assert.True(result[_editCipherId].Edit);
        Assert.True(result[_manageCipherId].Manage);
        Assert.True(result[_readExceptPasswordCipherId].Read);
        Assert.False(result[_readExceptPasswordCipherId].ViewPassword);

        Assert.True(result[_unassignedCipherId].Read);
        Assert.True(result[_unassignedCipherId].Edit);
        Assert.True(result[_unassignedCipherId].ViewPassword);
        Assert.True(result[_unassignedCipherId].Manage);
    }

    private List<OrganizationCipherPermission> CreateCipherPermissions()
    {
        // User has no relationship with the cipher
        var noAccessCipher = new OrganizationCipherPermission
        {
            Id = _noAccessCipherId,
            Read = false,
            Edit = false,
            Manage = false,
            ViewPassword = false,
        };

        var readOnlyCipher = new OrganizationCipherPermission
        {
            Id = _readOnlyCipherId,
            Read = true,
            Edit = false,
            Manage = false,
            ViewPassword = true,
        };

        var editCipher = new OrganizationCipherPermission
        {
            Id = _editCipherId,
            Read = true,
            Edit = true,
            Manage = false,
            ViewPassword = true,
        };

        var manageCipher = new OrganizationCipherPermission
        {
            Id = _manageCipherId,
            Read = true,
            Edit = true,
            Manage = true,
            ViewPassword = true,
        };

        var readExceptPasswordCipher = new OrganizationCipherPermission
        {
            Id = _readExceptPasswordCipherId,
            Read = true,
            Edit = false,
            Manage = false,
            ViewPassword = false,
        };

        var unassignedCipher = new OrganizationCipherPermission
        {
            Id = _unassignedCipherId,
            Read = false,
            Edit = false,
            Manage = false,
            ViewPassword = false,
        };

        return new List<OrganizationCipherPermission>
        {
            noAccessCipher,
            readOnlyCipher,
            editCipher,
            manageCipher,
            readExceptPasswordCipher,
            unassignedCipher
        };
    }
}
