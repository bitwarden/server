using Bit.Admin.AdminConsole.Controllers;
using Bit.Admin.AdminConsole.Models;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
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
        SponsoredPlanType = SponsoredPlanType.Premium,
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
            r.SponsoredPlanType == SponsoredPlanType.Premium &&
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
}
