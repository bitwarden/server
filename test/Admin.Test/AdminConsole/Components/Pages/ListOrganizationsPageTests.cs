using Bit.Admin.AdminConsole.Components.Pages.Organizations;
using Bit.Core.AdminConsole.Entities;
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

    [Fact]
    public void ListOrganizationsPage_PopulatesTable_WhenSearchFormIsSubmitted()
    {
        // Arrange
        var expectedOrganization = new Organization { Id = Guid.NewGuid(), Name = "Example" };
        _organizationRepository.SearchAsync(
            "Example",
            "johndoe@example.com",
            null,
            Arg.Any<int>(),
            Arg.Any<int>()).Returns(new List<Organization>
        {
            expectedOrganization
        });
        var cut = RenderComponent<ListOrganizationsPage>();

        // Act
        cut.Find("input[name='SearchForm.Name']").Change("Example");
        cut.Find("input[name='SearchForm.Email']").Change("johndoe@example.com");
        cut.Find("form").Submit();

        // Assert
        _organizationRepository.Received(1).SearchAsync(
            "Example",
            "johndoe@example.com",
            null,
            Arg.Any<int>(),
            Arg.Any<int>());

        var rows = cut.FindAll("tbody>tr");
        Assert.Single(rows);

        Assert.Contains(expectedOrganization.Name, rows[0].TextContent);
    }
}
