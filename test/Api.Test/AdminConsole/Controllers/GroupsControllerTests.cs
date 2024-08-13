using System.Security.Claims;
using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(GroupsController))]
[SutProviderCustomize]
public class GroupsControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task Post_AuthorizedToGiveAccessToCollections_Success(Organization organization,
        GroupRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        // Enable FC
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(
            new OrganizationAbility { Id = organization.Id, AllowAdminAccessToAllCollectionItems = false });

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                 Arg.Any<IEnumerable<Collection>>(),
                 Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ModifyGroupAccess)))
             .Returns(AuthorizationResult.Success());

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ManageGroups(organization.Id).Returns(true);

        var response = await sutProvider.Sut.Post(organization.Id, groupRequestModel);

        var requestModelCollectionIds = groupRequestModel.Collections.Select(c => c.Id).ToHashSet();

        // Assert that it checked permissions
        await sutProvider.GetDependency<ICurrentContext>().Received(1).ManageGroups(organization.Id);
        await sutProvider.GetDependency<IAuthorizationService>()
            .Received(1)
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Is<IEnumerable<Collection>>(collections =>
                    collections.All(c => requestModelCollectionIds.Contains(c.Id))),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Single() == BulkCollectionOperations.ModifyGroupAccess));

        // Assert that it saved the data
        await sutProvider.GetDependency<ICreateGroupCommand>().Received(1).CreateGroupAsync(
            Arg.Is<Group>(g =>
                g.OrganizationId == organization.Id && g.Name == groupRequestModel.Name),
            organization,
            Arg.Is<ICollection<CollectionAccessSelection>>(access =>
                access.All(c => requestModelCollectionIds.Contains(c.Id))),
            Arg.Any<IEnumerable<Guid>>());
        Assert.Equal(groupRequestModel.Name, response.Name);
        Assert.Equal(organization.Id, response.OrganizationId);
    }

    [Theory]
    [BitAutoData]
    public async Task Post_NotAuthorizedToGiveAccessToCollections_Throws(Organization organization, GroupRequestModel groupRequestModel, SutProvider<GroupsController> sutProvider)
    {
        // Enable FC
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(
            new OrganizationAbility { Id = organization.Id, AllowAdminAccessToAllCollectionItems = false });

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ManageGroups(organization.Id).Returns(true);

        var requestModelCollectionIds = groupRequestModel.Collections.Select(c => c.Id).ToHashSet();
        sutProvider.GetDependency<IAuthorizationService>()
           .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Is<IEnumerable<Collection>>(collections => collections.All(c => requestModelCollectionIds.Contains(c.Id))),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ModifyGroupAccess)))
            .Returns(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Post(organization.Id, groupRequestModel));

        await sutProvider.GetDependency<ICreateGroupCommand>().DidNotReceiveWithAnyArgs()
            .CreateGroupAsync(default, default, default, default);
    }
}
