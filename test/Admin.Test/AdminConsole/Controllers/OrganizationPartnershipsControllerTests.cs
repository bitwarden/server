using System.Reflection;
using Bit.Admin.AdminConsole.Controllers;
using Bit.Admin.AdminConsole.Models;
using Bit.Admin.Enums;
using Bit.Admin.Utilities;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;

namespace Admin.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(OrganizationPartnershipsController))]
[SutProviderCustomize]
public class OrganizationPartnershipsControllerTests
{
    private static void Arrange(SutProvider<OrganizationPartnershipsController> sutProvider, Organization organization)
    {
        sutProvider.GetDependency<Bitwarden.Server.Sdk.Features.IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PartnerSponsorships)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.Sut.TempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());
    }

    private static CreateOrganizationPartnershipModel ValidModel() => new()
    {
        Name = "Partner",
        AllowedSponsoredPlanTypes = [SponsoredPlanType.Premium],
        BindingMode = PartnershipBindingMode.Token,
        RegisteredReturnOrigins = "https://partner.example.com",
    };

    [Theory, BitAutoData]
    public async Task CreateGet_FeatureDisabled_ReturnsNotFound(
        Organization organization,
        SutProvider<OrganizationPartnershipsController> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var result = await sutProvider.Sut.Create(organization.Id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory, BitAutoData]
    public async Task CreatePost_FeatureDisabled_ReturnsNotFoundAndDoesNotCreate(
        Organization organization,
        SutProvider<OrganizationPartnershipsController> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var result = await sutProvider.Sut.Create(organization.Id, ValidModel());

        Assert.IsType<NotFoundResult>(result);
        await sutProvider.GetDependency<ICreateOrganizationPartnershipCommand>()
            .DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateGet_UnknownOrganization_ReturnsNotFound(
        Guid organizationId,
        SutProvider<OrganizationPartnershipsController> sutProvider)
    {
        sutProvider.GetDependency<Bitwarden.Server.Sdk.Features.IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PartnerSponsorships)
            .Returns(true);

        var result = await sutProvider.Sut.Create(organizationId);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory, BitAutoData]
    public async Task CreatePost_UnknownOrganization_ReturnsNotFoundAndDoesNotCreate(
        Guid organizationId,
        SutProvider<OrganizationPartnershipsController> sutProvider)
    {
        sutProvider.GetDependency<Bitwarden.Server.Sdk.Features.IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PartnerSponsorships)
            .Returns(true);

        var result = await sutProvider.Sut.Create(organizationId, ValidModel());

        Assert.IsType<NotFoundResult>(result);
        await sutProvider.GetDependency<ICreateOrganizationPartnershipCommand>()
            .DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateGet_NoPartnership_ReturnsFormForOrganization(
        Organization organization,
        SutProvider<OrganizationPartnershipsController> sutProvider)
    {
        Arrange(sutProvider, organization);

        var result = await sutProvider.Sut.Create(organization.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CreateOrganizationPartnershipModel>(view.Model);
        Assert.Equal(organization.Id, model.OrganizationId);
    }

    [Theory, BitAutoData]
    public async Task CreateGet_NoPartnership_PreselectsPremium(
        Organization organization,
        SutProvider<OrganizationPartnershipsController> sutProvider)
    {
        Arrange(sutProvider, organization);

        var result = await sutProvider.Sut.Create(organization.Id);

        var model = Assert.IsType<CreateOrganizationPartnershipModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal([SponsoredPlanType.Premium], model.AllowedSponsoredPlanTypes);
    }

    [Theory, BitAutoData]
    public async Task CreateGet_ExistingPartnership_RedirectsToOrganizationWithError(
        Organization organization,
        OrganizationPartnership partnership,
        SutProvider<OrganizationPartnershipsController> sutProvider)
    {
        Arrange(sutProvider, organization);
        sutProvider.GetDependency<IOrganizationPartnershipRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns(partnership);

        var result = await sutProvider.Sut.Create(organization.Id);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Organizations", redirect.ControllerName);
        Assert.Equal("Edit", redirect.ActionName);
        Assert.Equal(organization.Id, redirect.RouteValues!["id"]);
        Assert.Equal("This organization already has a partnership.", sutProvider.Sut.TempData["Error"]);
    }

    [Theory, BitAutoData]
    public async Task CreatePost_Valid_SendsTrimmedNonBlankOriginsToCommand(
        Organization organization,
        OrganizationPartnership partnership,
        SutProvider<OrganizationPartnershipsController> sutProvider)
    {
        Arrange(sutProvider, organization);
        var command = sutProvider.GetDependency<ICreateOrganizationPartnershipCommand>();
        command.CreateAsync(Arg.Any<CreateOrganizationPartnershipRequest>()).Returns(partnership);
        var model = ValidModel();
        model.Name = "  Partner  ";
        model.RegisteredReturnOrigins = "  https://a.example.com \r\n\r\n   \nhttps://b.example.com:8443\t\n";

        await sutProvider.Sut.Create(organization.Id, model);

        await command.Received(1).CreateAsync(Arg.Is<CreateOrganizationPartnershipRequest>(r =>
            r.OrganizationId == organization.Id &&
            r.Name == "Partner" &&
            r.AllowedSponsoredPlanTypes.SequenceEqual(new[] { SponsoredPlanType.Premium }) &&
            r.BindingMode == PartnershipBindingMode.Token &&
            r.RegisteredReturnOrigins.SequenceEqual(new[] { "https://a.example.com", "https://b.example.com:8443" })));
    }

    [Theory, BitAutoData]
    public async Task CreatePost_Success_RedirectsToOrganizationWithSuccessMessage(
        Organization organization,
        OrganizationPartnership partnership,
        SutProvider<OrganizationPartnershipsController> sutProvider)
    {
        Arrange(sutProvider, organization);
        sutProvider.GetDependency<ICreateOrganizationPartnershipCommand>()
            .CreateAsync(Arg.Any<CreateOrganizationPartnershipRequest>())
            .Returns(partnership);

        var result = await sutProvider.Sut.Create(organization.Id, ValidModel());

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Organizations", redirect.ControllerName);
        Assert.Equal("Edit", redirect.ActionName);
        Assert.Equal(organization.Id, redirect.RouteValues!["id"]);
        Assert.Equal("Partnership created.", sutProvider.Sut.TempData["Success"]);
    }

    [Theory, BitAutoData]
    public async Task CreatePost_InvalidReturnOrigin_ReRendersFormWithOriginMessage(
        Organization organization,
        SutProvider<OrganizationPartnershipsController> sutProvider)
    {
        Arrange(sutProvider, organization);
        sutProvider.GetDependency<ICreateOrganizationPartnershipCommand>()
            .CreateAsync(Arg.Any<CreateOrganizationPartnershipRequest>())
            .Returns(new InvalidReturnOrigin());
        var model = ValidModel();

        var result = await sutProvider.Sut.Create(organization.Id, model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        var error = Assert.Single(sutProvider.Sut.ModelState[nameof(model.RegisteredReturnOrigins)]!.Errors);
        Assert.StartsWith("Each return origin must be an https origin", error.ErrorMessage);
        Assert.Null(sutProvider.Sut.TempData["Success"]);
    }

    [Theory, BitAutoData]
    public async Task CreatePost_NoAllowedPlanTypes_ReRendersFormWithPlanTypeMessage(
        Organization organization,
        SutProvider<OrganizationPartnershipsController> sutProvider)
    {
        Arrange(sutProvider, organization);
        sutProvider.GetDependency<ICreateOrganizationPartnershipCommand>()
            .CreateAsync(Arg.Any<CreateOrganizationPartnershipRequest>())
            .Returns(new AllowedSponsoredPlanTypesRequired());
        var model = ValidModel();
        model.AllowedSponsoredPlanTypes = [];

        var result = await sutProvider.Sut.Create(organization.Id, model);

        Assert.IsType<ViewResult>(result);
        var error = Assert.Single(sutProvider.Sut.ModelState[nameof(model.AllowedSponsoredPlanTypes)]!.Errors);
        Assert.Equal("Choose at least one allowed sponsored plan type.", error.ErrorMessage);
    }

    [Theory, BitAutoData]
    public async Task CreatePost_PartnershipAlreadyExists_ReRendersFormWithSummaryMessage(
        Organization organization,
        SutProvider<OrganizationPartnershipsController> sutProvider)
    {
        Arrange(sutProvider, organization);
        sutProvider.GetDependency<ICreateOrganizationPartnershipCommand>()
            .CreateAsync(Arg.Any<CreateOrganizationPartnershipRequest>())
            .Returns(new PartnershipAlreadyExists());

        var result = await sutProvider.Sut.Create(organization.Id, ValidModel());

        Assert.IsType<ViewResult>(result);
        var error = Assert.Single(sutProvider.Sut.ModelState[string.Empty]!.Errors);
        Assert.Equal("This organization already has a partnership.", error.ErrorMessage);
    }

    [Theory, BitAutoData]
    public async Task CreatePost_InvalidModelState_ReRendersFormWithoutCallingCommand(
        Organization organization,
        SutProvider<OrganizationPartnershipsController> sutProvider)
    {
        Arrange(sutProvider, organization);
        sutProvider.Sut.ModelState.AddModelError(nameof(CreateOrganizationPartnershipModel.Name), "required");

        var result = await sutProvider.Sut.Create(organization.Id, new CreateOrganizationPartnershipModel());

        Assert.IsType<ViewResult>(result);
        await sutProvider.GetDependency<ICreateOrganizationPartnershipCommand>()
            .DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task Deactivate_Success_SetsInactiveAndRedirectsWithMessage(
        Organization organization,
        OrganizationPartnership partnership,
        SutProvider<OrganizationPartnershipsController> sutProvider)
    {
        Arrange(sutProvider, organization);
        var command = sutProvider.GetDependency<ISetOrganizationPartnershipStatusCommand>();
        command.SetStatusAsync(organization.Id, PartnershipStatus.Inactive).Returns(partnership);

        var result = await sutProvider.Sut.Deactivate(organization.Id);

        await command.Received(1).SetStatusAsync(organization.Id, PartnershipStatus.Inactive);
        AssertRedirectToOrganization(result, organization.Id);
        Assert.Equal("Partnership deactivated.", sutProvider.Sut.TempData["Success"]);
    }

    [Theory, BitAutoData]
    public async Task Reactivate_Success_SetsActiveAndRedirectsWithMessage(
        Organization organization,
        OrganizationPartnership partnership,
        SutProvider<OrganizationPartnershipsController> sutProvider)
    {
        Arrange(sutProvider, organization);
        var command = sutProvider.GetDependency<ISetOrganizationPartnershipStatusCommand>();
        command.SetStatusAsync(organization.Id, PartnershipStatus.Active).Returns(partnership);

        var result = await sutProvider.Sut.Reactivate(organization.Id);

        await command.Received(1).SetStatusAsync(organization.Id, PartnershipStatus.Active);
        AssertRedirectToOrganization(result, organization.Id);
        Assert.Equal("Partnership reactivated.", sutProvider.Sut.TempData["Success"]);
    }

    [Theory, BitAutoData]
    public async Task Deactivate_FeatureDisabled_ReturnsNotFoundWithoutCallingCommand(
        Guid organizationId,
        SutProvider<OrganizationPartnershipsController> sutProvider)
    {
        var result = await sutProvider.Sut.Deactivate(organizationId);

        Assert.IsType<NotFoundResult>(result);
        await sutProvider.GetDependency<ISetOrganizationPartnershipStatusCommand>()
            .DidNotReceiveWithAnyArgs().SetStatusAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task Reactivate_FeatureDisabled_ReturnsNotFoundWithoutCallingCommand(
        Guid organizationId,
        SutProvider<OrganizationPartnershipsController> sutProvider)
    {
        var result = await sutProvider.Sut.Reactivate(organizationId);

        Assert.IsType<NotFoundResult>(result);
        await sutProvider.GetDependency<ISetOrganizationPartnershipStatusCommand>()
            .DidNotReceiveWithAnyArgs().SetStatusAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task Deactivate_PartnershipNotFound_ReturnsNotFound(
        Organization organization,
        SutProvider<OrganizationPartnershipsController> sutProvider)
    {
        Arrange(sutProvider, organization);
        sutProvider.GetDependency<ISetOrganizationPartnershipStatusCommand>()
            .SetStatusAsync(organization.Id, PartnershipStatus.Inactive)
            .Returns(new PartnershipNotFound());

        var result = await sutProvider.Sut.Deactivate(organization.Id);

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(sutProvider.Sut.TempData["Success"]);
    }

    [Theory, BitAutoData]
    public async Task Reactivate_PartnershipNotFound_ReturnsNotFound(
        Organization organization,
        SutProvider<OrganizationPartnershipsController> sutProvider)
    {
        Arrange(sutProvider, organization);
        sutProvider.GetDependency<ISetOrganizationPartnershipStatusCommand>()
            .SetStatusAsync(organization.Id, PartnershipStatus.Active)
            .Returns(new PartnershipNotFound());

        var result = await sutProvider.Sut.Reactivate(organization.Id);

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(sutProvider.Sut.TempData["Success"]);
    }

    [Theory]
    [InlineData(nameof(OrganizationPartnershipsController.Deactivate))]
    [InlineData(nameof(OrganizationPartnershipsController.Reactivate))]
    public void StatusAction_IsCloudOnlyAntiForgeryPostRequiringPlanEdit(string actionName)
    {
        var action = typeof(OrganizationPartnershipsController).GetMethod(actionName)!;

        Assert.NotNull(action.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Equal(Permission.Org_Plan_Edit, action.GetCustomAttribute<RequirePermissionAttribute>()!.Permission);
        Assert.True(action.GetCustomAttribute<SelfHostedAttribute>()!.NotSelfHostedOnly);
    }

    private static void AssertRedirectToOrganization(IActionResult result, Guid organizationId)
    {
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Organizations", redirect.ControllerName);
        Assert.Equal("Edit", redirect.ActionName);
        Assert.Equal(organizationId, redirect.RouteValues!["id"]);
    }
}
