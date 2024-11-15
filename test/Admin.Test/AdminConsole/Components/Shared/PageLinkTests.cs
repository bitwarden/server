using Bit.Admin.AdminConsole.Components.Shared.Table;
using Bunit;

namespace Admin.Test.AdminConsole.Components.Shared;

public class PageLinkTests : TestContext
{
    [Fact]
    public void PageLink_Renders_ClickableLinksCorrectlyWhenPageParameterIsSet()
    {
        // Arrange
        const string formKey = "Key";
        const string label = "Test";
        const int page = 1;

        // Act
        var cut = RenderComponent<PageLink>(
            (nameof(PageLink.FormKey), formKey),
            (nameof(PageLink.Label), label),
            (nameof(PageLink.Page), page)

        );

        // Assert
        var button = cut.Find("button");
        Assert.Equal(formKey, button.Attributes.Single(x => x.Name == "name").Value);
        Assert.Equal(label, button.InnerHtml);
        Assert.Equal(page.ToString(), button.Attributes.Single(x => x.Name == "value").Value);
    }

    [Fact]
    public void PageLink_Renders_ClickableLinksCorrectlyWhenPageParameterIsNotSet()
    {
        // Arrange
        const string formKey = "Key";
        const string label = "Test";
        int? page = null;

        // Act
        var cut = RenderComponent<PageLink>(
            (nameof(PageLink.FormKey), formKey),
            (nameof(PageLink.Label), label),
            (nameof(PageLink.Page), page)

        );

        // Assert
        var disabledButton = cut.Find("a");
        Assert.Equal("#", disabledButton.Attributes.Single(x => x.Name == "href").Value);
    }
}
