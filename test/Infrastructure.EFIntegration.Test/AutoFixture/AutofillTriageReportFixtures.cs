using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Autofill.Entities;
using Bit.Infrastructure.EntityFramework.Autofill.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Infrastructure.EFIntegration.Test.AutoFixture;

internal class AutofillTriageReportBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var type = request as Type;
        if (type == null || type != typeof(AutofillTriageReport))
        {
            return new NoSpecimen();
        }

        var fixture = new Fixture();
        var obj = fixture.Build<AutofillTriageReport>()
            .With(r => r.PageUrl, "https://example.com/login")
            .With(r => r.ReportData, "{\"fields\":[]}")
            .With(r => r.Archived, false)
            .Create();
        return obj;
    }
}

internal class EfAutofillTriageReport : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
        fixture.Customizations.Add(new GlobalSettingsBuilder());
        fixture.Customizations.Add(new AutofillTriageReportBuilder());
        fixture.Customizations.Add(new EfRepositoryListBuilder<AutofillTriageReportRepository>());
    }
}

internal class EfAutofillTriageReportAutoDataAttribute : CustomAutoDataAttribute
{
    public EfAutofillTriageReportAutoDataAttribute() : base(new SutProviderCustomization(), new EfAutofillTriageReport())
    { }
}
