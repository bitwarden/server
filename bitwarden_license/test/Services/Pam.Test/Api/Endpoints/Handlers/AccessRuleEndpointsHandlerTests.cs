using System.Text.Json;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Api.Endpoints.Handlers;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Endpoints.Handlers;

/// <summary>
/// Whether the caller may touch the organization at all is settled by the authorization middleware before a handler
/// runs (see <c>AccessRuleEndpoints</c>). What is left to the handler — and so what these tests pin — is resource
/// scoping, that a rule reached by ID belongs to the organization on the route, and the edit attribution handed to
/// the commands.
/// </summary>
[SutProviderCustomize]
public class AccessRuleEndpointsHandlerTests
{
    [Theory, BitAutoData]
    public async Task GetAll_ReturnsTheOrganizationsRules(
        Guid organizationId,
        AccessRuleDetails first,
        AccessRuleDetails second,
        SutProvider<AccessRuleEndpointsHandler> sutProvider)
    {
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyDetailsByOrganizationIdAsync(organizationId)
            .Returns(new List<AccessRuleDetails> { first, second });

        var result = await sutProvider.Sut.GetAll(organizationId);

        Assert.Equal(new[] { first.Id, second.Id }, result.Data.Select(rule => rule.Id).ToArray());
    }

    [Theory, BitAutoData]
    public async Task Get_ReturnsTheRule(
        AccessRuleDetails rule, SutProvider<AccessRuleEndpointsHandler> sutProvider)
    {
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetDetailsByIdAsync(rule.Id)
            .Returns(rule);

        var result = await sutProvider.Sut.Get(rule.OrganizationId, rule.Id);

        Assert.Equal(rule.Id, result.Id);
    }

    /// <summary>
    /// Membership in the route's organization is all the middleware establishes, so nothing but this check stops a
    /// rule ID from one organization being read through another organization's route.
    /// </summary>
    [Theory, BitAutoData]
    public async Task Get_ARuleBelongingToAnotherOrganization_ThrowsNotFound(
        AccessRuleDetails rule, Guid otherOrganizationId, SutProvider<AccessRuleEndpointsHandler> sutProvider)
    {
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetDetailsByIdAsync(rule.Id)
            .Returns(rule);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Get(otherOrganizationId, rule.Id));
    }

    [Theory, BitAutoData]
    public async Task Get_AMissingRule_ThrowsNotFound(
        Guid organizationId, Guid id, SutProvider<AccessRuleEndpointsHandler> sutProvider)
    {
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetDetailsByIdAsync(id)
            .Returns((AccessRuleDetails?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Get(organizationId, id));
    }

    [Theory, BitAutoData]
    public async Task Post_CreatesTheRuleForTheRouteOrganization_StampedWithTheCallingUser(
        Guid organizationId,
        Guid userId,
        AccessRuleDetails created,
        SutProvider<AccessRuleEndpointsHandler> sutProvider)
    {
        var model = RequestModel();
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICreateAccessRuleCommand>()
            .CreateAsync(Arg.Any<AccessRule>(), Arg.Any<IEnumerable<Guid>>())
            .Returns(created);

        await sutProvider.Sut.Post(organizationId, model);

        await sutProvider.GetDependency<ICreateAccessRuleCommand>().Received(1)
            .CreateAsync(
                Arg.Is<AccessRule>(rule => rule.OrganizationId == organizationId && rule.LastEditedBy == userId),
                model.Collections);
    }

    [Theory, BitAutoData]
    public async Task Put_UpdatesTheRule_StampedWithTheCallingUser(
        Guid organizationId,
        Guid id,
        Guid userId,
        AccessRuleDetails updated,
        SutProvider<AccessRuleEndpointsHandler> sutProvider)
    {
        var model = RequestModel();
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<IUpdateAccessRuleCommand>()
            .UpdateAsync(organizationId, id, Arg.Any<AccessRule>(), Arg.Any<IEnumerable<Guid>>())
            .Returns(updated);

        await sutProvider.Sut.Put(organizationId, id, model);

        await sutProvider.GetDependency<IUpdateAccessRuleCommand>().Received(1)
            .UpdateAsync(
                organizationId,
                id,
                Arg.Is<AccessRule>(rule => rule.OrganizationId == organizationId && rule.LastEditedBy == userId),
                model.Collections);
    }

    /// <summary>
    /// The route's organization is what scopes the delete — the command rejects an ID belonging to any other.
    /// </summary>
    [Theory, BitAutoData]
    public async Task Delete_DeletesWithinTheRouteOrganization_StampedWithTheCallingUser(
        Guid organizationId, Guid id, Guid userId, SutProvider<AccessRuleEndpointsHandler> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);

        await sutProvider.Sut.Delete(organizationId, id);

        // The caller is passed through because the delete is hard: the audit event is the only record of who did it.
        await sutProvider.GetDependency<IDeleteAccessRuleCommand>().Received(1)
            .DeleteAsync(organizationId, id, userId);
    }

    private static AccessRuleRequestModel RequestModel() => new()
    {
        Name = "Production database",
        Conditions = JsonDocument.Parse("[]").RootElement,
        Collections = new List<Guid> { Guid.NewGuid() },
    };
}
