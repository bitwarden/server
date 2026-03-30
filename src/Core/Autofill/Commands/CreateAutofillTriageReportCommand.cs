using Bit.Core.Autofill.Entities;
using Bit.Core.Autofill.Repositories;

namespace Bit.Core.Autofill.Commands;

public interface ICreateAutofillTriageReportCommand
{
    Task Run(AutofillTriageReport report);
}

public class CreateAutofillTriageReportCommand(IAutofillTriageReportRepository repository)
    : ICreateAutofillTriageReportCommand
{
    public async Task Run(AutofillTriageReport report)
    {
        report.SetNewId();
        await repository.CreateAsync(report);
    }
}
