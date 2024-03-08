using Bit.Api.AdminConsole.Public.Controllers;
using Bit.Api.AdminConsole.Public.Models.Request;
using Bit.Api.AdminConsole.Public.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Public.Controllers;

[ControllerCustomize(typeof(GroupsController))]
[SutProviderCustomize]
public class GroupsControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task Post_Success_BeforeFlexibleCollectionMigration(Organization organization, GroupCreateUpdateRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        // Organization has not migrated
        organization.FlexibleCollections = false;

        // Permissions do not contain Manage property
        var expectedPermissions = (groupRequestModel.Collections ?? []).Select(model => new AssociationWithPermissionsRequestModel { Id = model.Id, ReadOnly = model.ReadOnly, HidePasswords = model.HidePasswords.GetValueOrDefault() });
        groupRequestModel.Collections = expectedPermissions;

        sutProvider.GetDependency<ICurrentContext>().OrganizationId.Returns(organization.Id);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var response = await sutProvider.Sut.Post(groupRequestModel) as JsonResult;
        var responseValue = response.Value as GroupResponseModel;

        await sutProvider.GetDependency<ICreateGroupCommand>().Received(1).CreateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name &&
                g.AccessAll == groupRequestModel.AccessAll && g.ExternalId == groupRequestModel.ExternalId),
            organization,
            Arg.Any<ICollection<CollectionAccessSelection>>());

        Assert.Equal(groupRequestModel.Name, responseValue.Name);
        Assert.Equal(groupRequestModel.AccessAll, responseValue.AccessAll);
        Assert.Equal(groupRequestModel.ExternalId, responseValue.ExternalId);
    }

    [Theory]
    [BitAutoData]
    public async Task Post_Throws_BadRequestException_BeforeFlexibleCollectionMigration_Manage(Organization organization, GroupCreateUpdateRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        // Organization has not migrated
        organization.FlexibleCollections = false;

        // Contains at least one can manage
        groupRequestModel.Collections.First().Manage = true;

        sutProvider.GetDependency<ICurrentContext>().OrganizationId.Returns(organization.Id);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        await sutProvider.GetDependency<ICreateGroupCommand>().DidNotReceiveWithAnyArgs().CreateGroupAsync(default, default, default, default);
        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.Post(groupRequestModel));
    }

    [Theory]
    [BitAutoData]
    public async Task Put_Success_BeforeFlexibleCollectionMigration(Organization organization, Group group, GroupCreateUpdateRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        // Organization has not migrated
        organization.FlexibleCollections = false;

        // Permissions do not contain Manage property
        var expectedPermissions = (groupRequestModel.Collections ?? []).Select(model => new AssociationWithPermissionsRequestModel { Id = model.Id, ReadOnly = model.ReadOnly, HidePasswords = model.HidePasswords.GetValueOrDefault() });
        groupRequestModel.Collections = expectedPermissions;

        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).Returns(group);
        sutProvider.GetDependency<ICurrentContext>().OrganizationId.Returns(organization.Id);

        var response = await sutProvider.Sut.Put(group.Id, groupRequestModel) as JsonResult;
        var responseValue = response.Value as GroupResponseModel;

        await sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).UpdateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name &&
                g.AccessAll == groupRequestModel.AccessAll && g.ExternalId == groupRequestModel.ExternalId),
            Arg.Is<Organization>(o => o.Id == organization.Id),
            Arg.Any<ICollection<CollectionAccessSelection>>());

        Assert.Equal(groupRequestModel.Name, responseValue.Name);
        Assert.Equal(groupRequestModel.AccessAll, responseValue.AccessAll);
        Assert.Equal(groupRequestModel.ExternalId, responseValue.ExternalId);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_Throws_BadRequestException_BeforeFlexibleCollectionMigration_Manage(Organization organization, Group group, GroupCreateUpdateRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        // Organization has not migrated
        organization.FlexibleCollections = false;

        // Contains at least one can manage
        groupRequestModel.Collections.First().Manage = true;

        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).Returns(group);
        sutProvider.GetDependency<ICurrentContext>().OrganizationId.Returns(organization.Id);

        await sutProvider.GetDependency<IUpdateGroupCommand>().DidNotReceiveWithAnyArgs().UpdateGroupAsync(default, default, default, default);
        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.Put(group.Id, groupRequestModel));
    }

    [Theory]
    [BitAutoData]
    public async Task Post_Success_AfterFlexibleCollectionMigration(Organization organization, GroupCreateUpdateRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        // Organization has migrated
        organization.FlexibleCollections = true;

        // Contains at least one can manage
        groupRequestModel.Collections.First().Manage = true;

        sutProvider.GetDependency<ICurrentContext>().OrganizationId.Returns(organization.Id);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var response = await sutProvider.Sut.Post(groupRequestModel) as JsonResult;
        var responseValue = response.Value as GroupResponseModel;

        await sutProvider.GetDependency<ICreateGroupCommand>().Received(1).CreateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name &&
                g.AccessAll == groupRequestModel.AccessAll && g.ExternalId == groupRequestModel.ExternalId),
            organization,
            Arg.Any<ICollection<CollectionAccessSelection>>());

        Assert.Equal(groupRequestModel.Name, responseValue.Name);
        Assert.Equal(groupRequestModel.AccessAll, responseValue.AccessAll);
        Assert.Equal(groupRequestModel.ExternalId, responseValue.ExternalId);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_Success_AfterFlexibleCollectionMigration(Organization organization, Group group, GroupCreateUpdateRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        // Organization has migrated
        organization.FlexibleCollections = true;

        // Contains at least one can manage
        groupRequestModel.Collections.First().Manage = true;

        group.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(group.Id).Returns(group);
        sutProvider.GetDependency<ICurrentContext>().OrganizationId.Returns(organization.Id);

        var response = await sutProvider.Sut.Put(group.Id, groupRequestModel) as JsonResult;
        var responseValue = response.Value as GroupResponseModel;

        await sutProvider.GetDependency<IUpdateGroupCommand>().Received(1).UpdateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name &&
                g.AccessAll == groupRequestModel.AccessAll && g.ExternalId == groupRequestModel.ExternalId),
            Arg.Is<Organization>(o => o.Id == organization.Id),
            Arg.Any<ICollection<CollectionAccessSelection>>());

        Assert.Equal(groupRequestModel.Name, responseValue.Name);
        Assert.Equal(groupRequestModel.AccessAll, responseValue.AccessAll);
        Assert.Equal(groupRequestModel.ExternalId, responseValue.ExternalId);
    }
}
