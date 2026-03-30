using Bit.Api.Autofill.Controllers;
using Bit.Api.Autofill.Models;
using Bit.Core.Autofill.Commands;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Autofill.Controllers;

[ControllerCustomize(typeof(AutofillTriageReportController))]
[SutProviderCustomize]
public class AutofillTriageReportControllerTests
{
    [Theory, BitAutoData]
    public async Task Post_CallsCommandAndReturnsNoContent(
        AutofillTriageReportRequestModel model,
        SutProvider<AutofillTriageReportController> sutProvider)
    {
        var result = await sutProvider.Sut.Post(model);

        await sutProvider.GetDependency<ICreateAutofillTriageReportCommand>()
            .Received(1)
            .Run(Arg.Is<Core.Autofill.Entities.AutofillTriageReport>(r =>
                r.PageUrl == model.PageUrl &&
                r.ReportData == model.ReportData));

        Assert.IsType<NoContentResult>(result);
    }
}
