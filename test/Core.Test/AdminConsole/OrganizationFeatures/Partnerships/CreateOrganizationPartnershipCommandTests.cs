using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Partnerships;

[SutProviderCustomize]
public class CreateOrganizationPartnershipCommandTests
{
    [Theory, BitAutoData]
    public async Task CreateAsync_ValidRequest_CreatesActivePartnership(
        Organization organization, SutProvider<CreateOrganizationPartnershipCommand> sutProvider)
    {
        ArrangeOrganization(sutProvider, organization);

        var result = await sutProvider.Sut.CreateAsync(CreateRequest(organization, "https://partner.example.com"));

        var partnership = result.AsSuccess;
        Assert.NotEqual(Guid.Empty, partnership.Id);
        Assert.Equal(organization.Id, partnership.OrganizationId);
        Assert.Equal(PartnershipStatus.Active, partnership.Status);
        Assert.Equal(PartnershipBindingMode.Token, partnership.BindingMode);
        Assert.Equal(["https://partner.example.com"], partnership.GetRegisteredReturnOrigins());
        await sutProvider.GetDependency<IOrganizationPartnershipRepository>().Received(1).CreateAsync(partnership);
    }

    [Theory]
    [BitAutoData("https://partner.example.com")]
    [BitAutoData("https://partner.example.com:8443")]
    [BitAutoData("https://localhost:5001")]
    public async Task CreateAsync_HttpsOrigin_IsAccepted(
        string origin, Organization organization, SutProvider<CreateOrganizationPartnershipCommand> sutProvider)
    {
        ArrangeOrganization(sutProvider, organization);

        var result = await sutProvider.Sut.CreateAsync(CreateRequest(organization, origin));

        Assert.Equal([origin], result.AsSuccess.GetRegisteredReturnOrigins());
    }

    [Theory]
    [BitAutoData("http://partner.example.com")]
    [BitAutoData("https://partner.example.com/return")]
    [BitAutoData("https://partner.example.com/")]
    [BitAutoData("https://partner.example.com?next=1")]
    [BitAutoData("https://partner.example.com#fragment")]
    [BitAutoData("https://user@partner.example.com")]
    [BitAutoData("partner.example.com")]
    [BitAutoData("")]
    public async Task CreateAsync_InvalidOrigin_ReturnsValidationError(
        string origin, Organization organization, SutProvider<CreateOrganizationPartnershipCommand> sutProvider)
    {
        ArrangeOrganization(sutProvider, organization);

        var result = await sutProvider.Sut.CreateAsync(CreateRequest(organization, origin));

        Assert.Equal("invalid_return_origin", Assert.IsType<InvalidReturnOrigin>(result.AsError).Code);
        await sutProvider.GetDependency<IOrganizationPartnershipRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_OidcBindingMode_ReturnsValidationError(
        Organization organization, SutProvider<CreateOrganizationPartnershipCommand> sutProvider)
    {
        ArrangeOrganization(sutProvider, organization);

        var result = await sutProvider.Sut.CreateAsync(
            CreateRequest(organization, "https://partner.example.com") with { BindingMode = PartnershipBindingMode.Oidc });

        Assert.Equal("unsupported_binding_mode", Assert.IsType<UnsupportedBindingMode>(result.AsError).Code);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_UndefinedSponsoredPlanType_ReturnsValidationError(
        Organization organization, SutProvider<CreateOrganizationPartnershipCommand> sutProvider)
    {
        ArrangeOrganization(sutProvider, organization);

        var result = await sutProvider.Sut.CreateAsync(
            CreateRequest(organization, "https://partner.example.com") with { SponsoredPlanType = (SponsoredPlanType)5 });

        Assert.Equal("unsupported_sponsored_plan_type", Assert.IsType<UnsupportedSponsoredPlanType>(result.AsError).Code);
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData("   ")]
    public async Task CreateAsync_BlankName_ReturnsValidationError(
        string name, Organization organization, SutProvider<CreateOrganizationPartnershipCommand> sutProvider)
    {
        ArrangeOrganization(sutProvider, organization);

        var result = await sutProvider.Sut.CreateAsync(
            CreateRequest(organization, "https://partner.example.com") with { Name = name });

        Assert.IsType<InvalidPartnershipName>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_NameOverMaxLength_ReturnsValidationError(
        Organization organization, SutProvider<CreateOrganizationPartnershipCommand> sutProvider)
    {
        ArrangeOrganization(sutProvider, organization);
        var name = new string('a', CreateOrganizationPartnershipRequest.NameMaxLength + 1);

        var result = await sutProvider.Sut.CreateAsync(
            CreateRequest(organization, "https://partner.example.com") with { Name = name });

        Assert.IsType<InvalidPartnershipName>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_OrganizationNotFound_ReturnsNotFound(
        Organization organization, SutProvider<CreateOrganizationPartnershipCommand> sutProvider)
    {
        var result = await sutProvider.Sut.CreateAsync(CreateRequest(organization, "https://partner.example.com"));

        Assert.Equal("not_found", Assert.IsType<OrganizationNotFound>(result.AsError).Code);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_OrganizationHasPartnership_ReturnsConflict(
        Organization organization, OrganizationPartnership existing,
        SutProvider<CreateOrganizationPartnershipCommand> sutProvider)
    {
        ArrangeOrganization(sutProvider, organization);
        sutProvider.GetDependency<IOrganizationPartnershipRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns(existing);

        var result = await sutProvider.Sut.CreateAsync(CreateRequest(organization, "https://partner.example.com"));

        Assert.IsType<PartnershipAlreadyExists>(result.AsError);
        await sutProvider.GetDependency<IOrganizationPartnershipRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    private static void ArrangeOrganization(
        SutProvider<CreateOrganizationPartnershipCommand> sutProvider, Organization organization) =>
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

    private static CreateOrganizationPartnershipRequest CreateRequest(Organization organization, string origin) =>
        new()
        {
            OrganizationId = organization.Id,
            Name = "Partner",
            SponsoredPlanType = SponsoredPlanType.Premium,
            BindingMode = PartnershipBindingMode.Token,
            RegisteredReturnOrigins = [origin],
        };
}
