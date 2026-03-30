using Bit.Api.Autofill.Models;
using Xunit;

namespace Bit.Api.Test.Autofill.Models;

public class AutofillTriageReportRequestModelTests
{
    [Fact]
    public void ToEntity_MapsAllProperties()
    {
        var model = new AutofillTriageReportRequestModel
        {
            PageUrl = "https://example.com/login",
            TargetElementRef = "username",
            UserMessage = "Something went wrong",
            ReportData = "{\"fields\":[]}",
            ExtensionVersion = "2025.3.0",
        };

        var entity = model.ToEntity();

        Assert.Equal(model.PageUrl, entity.PageUrl);
        Assert.Equal(model.TargetElementRef, entity.TargetElementRef);
        Assert.Equal(model.UserMessage, entity.UserMessage);
        Assert.Equal(model.ReportData, entity.ReportData);
        Assert.Equal(model.ExtensionVersion, entity.ExtensionVersion);
    }

    [Fact]
    public void ToEntity_WithNullOptionals_MapsCorrectly()
    {
        var model = new AutofillTriageReportRequestModel
        {
            PageUrl = "https://example.com/login",
            ReportData = "{}",
            ExtensionVersion = "2025.3.0",
        };

        var entity = model.ToEntity();

        Assert.Equal(model.PageUrl, entity.PageUrl);
        Assert.Equal(model.ReportData, entity.ReportData);
        Assert.Equal(model.ExtensionVersion, entity.ExtensionVersion);
        Assert.Null(entity.TargetElementRef);
        Assert.Null(entity.UserMessage);
    }
}
