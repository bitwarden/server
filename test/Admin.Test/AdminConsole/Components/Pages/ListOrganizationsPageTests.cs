using Bit.Admin.AdminConsole.Components.Pages.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Admin.Test.AdminConsole.Components.Pages;

public class ListOrganizationsPageTests : TestContext
{
    private IOrganizationRepository _organizationRepository;
    private IGlobalSettings _globalSettings;

    public ListOrganizationsPageTests()
    {
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _globalSettings = Substitute.For<IGlobalSettings>();

        Services.AddSingleton(_organizationRepository);
        Services.AddSingleton(_globalSettings);
    }

    [Fact]
    public void ListOrganizationsPage_Renders_EmailFieldAsInvalid_WhenInvalidEmailAddressIsEntered()
    {
        // Arrange
        var cut = RenderComponent<ListOrganizationsPage>();

        // Act

        cut.Find("input[name='SearchForm.Email']").Change("@@");
        cut.Find("form").Submit();

        // Assert
        Assert.Contains("invalid", cut.Find("input[name='SearchForm.Email']").Attributes["class"].Value);
    }
}
