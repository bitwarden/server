using Bit.Core.Autofill.Commands;
using Bit.Core.Autofill.Entities;
using Bit.Core.Autofill.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Autofill.Commands;

[SutProviderCustomize]
public class CreateAutofillTriageReportCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task Run_CallsRepositoryCreateAsync(
        AutofillTriageReport report,
        SutProvider<CreateAutofillTriageReportCommand> sutProvider)
    {
        await sutProvider.Sut.Run(report);

        await sutProvider.GetDependency<IAutofillTriageReportRepository>()
            .Received(1)
            .CreateAsync(report);
    }

    [Theory]
    [BitAutoData]
    public async Task Run_SetsNewIdBeforeCreating(
        SutProvider<CreateAutofillTriageReportCommand> sutProvider)
    {
        var report = new AutofillTriageReport
        {
            PageUrl = "https://example.com",
            ReportData = "{}",
            ExtensionVersion = "2025.3.0",
        };

        await sutProvider.Sut.Run(report);

        Assert.NotEqual(Guid.Empty, report.Id);
        await sutProvider.GetDependency<IAutofillTriageReportRepository>()
            .Received(1)
            .CreateAsync(Arg.Is<AutofillTriageReport>(r => r.Id != Guid.Empty));
    }
}
